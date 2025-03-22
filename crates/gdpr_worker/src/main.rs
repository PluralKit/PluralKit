#![feature(let_chains)]

use sqlx::prelude::FromRow;
use std::{sync::Arc, time::Duration};
use tracing::{error, info, warn};
use twilight_http::api_error::{ApiError, GeneralApiError};
use twilight_model::id::{
    marker::{ChannelMarker, MessageMarker},
    Id,
};

// create table messages_gdpr_jobs (mid bigint not null references messages(mid) on delete cascade, channel bigint not null);

libpk::main!("messages_gdpr_worker");
async fn real_main() -> anyhow::Result<()> {
    let db = libpk::db::init_messages_db().await?;

    let mut client_builder = twilight_http::Client::builder()
        .token(
            libpk::config
                .discord
                .as_ref()
                .expect("missing discord config")
                .bot_token
                .clone(),
        )
        .timeout(Duration::from_secs(30));

    if let Some(base_url) = libpk::config
        .discord
        .as_ref()
        .expect("missing discord config")
        .api_base_url
        .clone()
    {
        client_builder = client_builder.proxy(base_url, true).ratelimiter(None);
    }

    let client = Arc::new(client_builder.build());

    loop {
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        match run_job(db.clone(), client.clone()).await {
            Ok(()) => {}
            Err(err) => {
                error!("failed to run messages gdpr job: {:?}", err);
            }
        }
    }
}

#[derive(FromRow)]
struct GdprJobEntry {
    mid: i64,
    channel_id: i64,
}

async fn run_job(pool: sqlx::PgPool, discord: Arc<twilight_http::Client>) -> anyhow::Result<()> {
    let mut tx = pool.begin().await?;

    let message: Option<GdprJobEntry> = sqlx::query_as(
        "select mid, channel_id from messages_gdpr_jobs for update skip locked limit 1;",
    )
    .fetch_optional(&mut *tx)
    .await?;

    let Some(message) = message else {
        info!("no job to run, sleeping for 1 minute");
        tokio::time::sleep(tokio::time::Duration::from_secs(60)).await;
        return Ok(());
    };

    info!("got mid={}, cleaning up...", message.mid);

    // naively delete message on discord's end
    let res = discord
        .delete_message(
            Id::<ChannelMarker>::new(message.channel_id as u64),
            Id::<MessageMarker>::new(message.mid as u64),
        )
        .await;

    if res.is_ok() {
        sqlx::query("delete from messages_gdpr_jobs where mid = $1")
            .bind(message.mid)
            .execute(&mut *tx)
            .await?;
    }

    if let Err(err) = res {
        if let twilight_http::error::ErrorType::Response { error, status, .. } = err.kind()
            && let ApiError::General(GeneralApiError { code, .. }) = error
        {
            match (status.get(), code) {
                (403, _) => {
                    warn!(
                        "got 403 while deleting message in channel {}, failing fast",
                        message.channel_id
                    );
                    sqlx::query("delete from messages_gdpr_jobs where channel_id = $1")
                        .bind(message.channel_id)
                        .execute(&mut *tx)
                        .await?;
                }
                (_, 10003) => {
                    warn!(
                        "deleting message in channel {}: channel not found, failing fast",
                        message.channel_id
                    );
                    sqlx::query("delete from messages_gdpr_jobs where channel_id = $1")
                        .bind(message.channel_id)
                        .execute(&mut *tx)
                        .await?;
                }
                (_, 10008) => {
                    warn!("deleting message {}: message not found", message.mid);
                    sqlx::query("delete from messages_gdpr_jobs where mid = $1")
                        .bind(message.mid)
                        .execute(&mut *tx)
                        .await?;
                }
                (_, 50083) => {
                    warn!(
                        "could not delete message in thread {}: thread is archived, failing fast",
                        message.channel_id
                    );
                    sqlx::query("delete from messages_gdpr_jobs where channel_id = $1")
                        .bind(message.channel_id)
                        .execute(&mut *tx)
                        .await?;
                }
                _ => {
                    error!(
                        "got unknown error deleting message {}: status={status}, code={code}",
                        message.mid
                    );
                }
            }
        } else {
            return Err(err.into());
        }
    }

    tx.commit().await?;

    return Ok(());
}
