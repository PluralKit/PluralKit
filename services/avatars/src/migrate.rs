use crate::pull::parse_url;
use crate::{db, process, AppState, PKAvatarError};
use libpk::db::types::avatars::{ImageMeta, ImageQueueEntry};
use reqwest::StatusCode;
use std::error::Error;
use std::sync::Arc;
use std::time::Duration;
use time::Instant;
use tokio::sync::Semaphore;
use tracing::{error, info, instrument, warn};

static PROCESS_SEMAPHORE: Semaphore = Semaphore::const_new(100);

pub async fn handle_item_inner(
    state: &AppState,
    item: &ImageQueueEntry,
) -> Result<(), PKAvatarError> {
    let parsed = parse_url(&item.url).map_err(|_| PKAvatarError::InvalidCdnUrl)?;

    if let Some(_) = db::get_by_attachment_id(&state.pool, parsed.attachment_id).await? {
        info!(
            "attachment {} already migrated, skipping",
            parsed.attachment_id
        );
        return Ok(());
    }

    let pulled = crate::pull::pull(state.pull_client.clone(), &parsed).await?;
    let data_len = pulled.data.len();

    let encoded = {
        // Trying to reduce CPU load/potentially blocking the worker by adding a bottleneck on parallel encodes
        // no semaphore on the main api though, that one should ideally be low latency
        // todo: configurable?
        let time_before_semaphore = Instant::now();
        let permit = PROCESS_SEMAPHORE
            .acquire()
            .await
            .map_err(|e| PKAvatarError::InternalError(e.into()))?;
        let time_after_semaphore = Instant::now();
        let semaphore_time = time_after_semaphore - time_before_semaphore;
        if semaphore_time.whole_milliseconds() > 100 {
            warn!(
                "waited more than {} ms for process semaphore",
                semaphore_time.whole_milliseconds()
            );
        }

        let encoded = process::process_async(pulled.data, item.kind).await?;
        drop(permit);
        encoded
    };
    let store_res = crate::store::store(&state.bucket, &encoded).await?;
    let final_url = format!("{}{}", state.config.cdn_url, store_res.path);

    db::add_image(
        &state.pool,
        ImageMeta {
            id: store_res.id,
            url: final_url.clone(),
            content_type: encoded.format.mime_type().to_string(),
            original_url: Some(parsed.full_url),
            original_type: Some(pulled.content_type),
            original_file_size: Some(data_len as i32),
            original_attachment_id: Some(parsed.attachment_id as i64),
            file_size: encoded.data.len() as i32,
            width: encoded.width as i32,
            height: encoded.height as i32,
            kind: item.kind,
            uploaded_at: None,
            uploaded_by_account: None,
            uploaded_by_system: None,
        },
    )
    .await?;

    info!(
        "migrated {} ({}k -> {}k)",
        final_url,
        data_len,
        encoded.data.len()
    );
    Ok(())
}

pub async fn handle_item(state: &AppState) -> Result<(), PKAvatarError> {
    // let queue_length = db::get_queue_length(&state.pool).await?;
    // info!("migrate queue length: {}", queue_length);

    if let Some((mut tx, item)) = db::pop_queue(&state.pool).await? {
        match handle_item_inner(state, &item).await {
            Ok(_) => {
                tx.commit().await.map_err(Into::<anyhow::Error>::into)?;
                Ok(())
            }
            Err(
                // Errors that mean the image can't be migrated and doesn't need to be retried
                e @ (PKAvatarError::ImageDimensionsTooLarge(_, _)
                | PKAvatarError::UnknownImageFormat
                | PKAvatarError::UnsupportedImageFormat(_)
                | PKAvatarError::UnsupportedContentType(_)
                | PKAvatarError::ImageFileSizeTooLarge(_, _)
                | PKAvatarError::InvalidCdnUrl
                | PKAvatarError::BadCdnResponse(StatusCode::NOT_FOUND | StatusCode::FORBIDDEN)),
            ) => {
                warn!("error migrating {}, skipping: {}", item.url, e);
                tx.commit().await.map_err(Into::<anyhow::Error>::into)?;
                Ok(())
            }
            Err(e @ PKAvatarError::ImageFormatError(_)) => {
                // will add this item back to the end of the queue
                db::push_queue(&mut *tx, &item.url, item.kind).await?;
                tx.commit().await.map_err(Into::<anyhow::Error>::into)?;
                Err(e)
            }
            Err(e) => Err(e),
        }
    } else {
        tokio::time::sleep(Duration::from_secs(5)).await;
        Ok(())
    }
}

#[instrument(skip(state))]
pub async fn worker(worker_id: u32, state: Arc<AppState>) {
    info!("spawned migrate worker with id {}", worker_id);
    loop {
        match handle_item(&state).await {
            Ok(()) => {}
            Err(e) => {
                error!(
                    "error in migrate worker {}: {}",
                    worker_id,
                    e.source().unwrap_or(&e)
                );
                tokio::time::sleep(Duration::from_secs(5)).await;
            }
        }
    }
}

pub fn spawn_migrate_workers(state: Arc<AppState>, count: u32) {
    for i in 0..count {
        tokio::spawn(worker(i, state.clone()));
    }
}
