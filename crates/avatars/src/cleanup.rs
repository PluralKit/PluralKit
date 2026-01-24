use anyhow::Context;
use reqwest::{ClientBuilder, StatusCode, Url};
use std::{path::Path, sync::Arc, time::Duration};
use tracing::{error, info};
use uuid::Uuid;

#[libpk::main]
async fn main() -> anyhow::Result<()> {
    let config = libpk::config
        .avatars
        .as_ref()
        .expect("missing avatar service config");

    let bucket = {
        let region = s3::Region::Custom {
            region: "auto".to_string(),
            endpoint: config.s3.endpoint.to_string(),
        };

        let credentials = s3::creds::Credentials::new(
            Some(&config.s3.application_id),
            Some(&config.s3.application_key),
            None,
            None,
            None,
        )
        .unwrap();

        let bucket = s3::Bucket::new(&config.s3.bucket, region, credentials)?;

        Arc::new(bucket)
    };

    let pool = libpk::db::init_data_db().await?;

    loop {
        // no infinite loops
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        match cleanup_job(pool.clone()).await {
            Ok(()) => {}
            Err(error) => {
                error!(?error, "failed to run avatar cleanup job");
                // sentry
            }
        }
        match cleanup_hash_job(pool.clone(), bucket.clone()).await {
            Ok(()) => {}
            Err(error) => {
                error!(?error, "failed to run hash cleanup job");
                // sentry
            }
        }
    }
}

#[derive(sqlx::FromRow)]
struct CleanupJobEntry {
    id: Uuid,
    system_uuid: Uuid,
}

async fn cleanup_job(pool: sqlx::PgPool) -> anyhow::Result<()> {
    let mut tx = pool.begin().await?;

    let entry: Option<CleanupJobEntry> = sqlx::query_as(
        // no timestamp checking here
        // images are only added to the table after 24h
        r#"
                select id, system_uuid from image_cleanup_jobs
                for update skip locked limit 1;"#,
    )
    .fetch_optional(&mut *tx)
    .await?;
    if entry.is_none() {
        info!("no job to run, sleeping for 1 minute");
        tokio::time::sleep(tokio::time::Duration::from_secs(60)).await;
        return Ok(());
    }
    let entry = entry.unwrap();
    let image_id = entry.id;
    let system_uuid = entry.system_uuid;
    info!("got image {image_id}, cleaning up...");

    let image =
        libpk::db::repository::avatars::get_by_id(&pool, system_uuid.clone(), image_id.clone())
            .await?;
    if image.is_none() {
        // unsure how this can happen? there is a FK reference
        info!("image {image_id} was already deleted, skipping");
        sqlx::query("delete from image_cleanup_jobs where id = $1 and system_uuid = $2")
            .bind(image_id)
            .bind(system_uuid)
            .execute(&mut *tx)
            .await?;
        return Ok(());
    }
    let image = image.unwrap();

    let config = libpk::config
        .avatars
        .as_ref()
        .expect("missing avatar service config");

    if let Some(store_id) = config.fastly_store_id.as_ref() {
        let client = ClientBuilder::new()
            .connect_timeout(Duration::from_secs(3))
            .timeout(Duration::from_secs(3))
            .build()
            .context("error making client")?;

        let url = Url::parse(&image.data.url).expect("invalid url");
        let extension = Path::new(url.path())
            .extension()
            .and_then(|s| s.to_str())
            .unwrap_or("");
        let key = format!("{system_uuid}:{image_id}.{extension}");

        let kv_resp = client
            .delete(format!(
                "https://api.fastly.com/resources/stores/kv/{store_id}/keys/{key}"
            ))
            .header("Fastly-Key", config.fastly_token.as_ref().unwrap())
            .send()
            .await?;

        match kv_resp.status() {
            StatusCode::OK => {
                info!(
                    "successfully purged image {}:{}.{} from fastly kv",
                    system_uuid, image_id, extension
                );
            }
            _ => {
                let status = kv_resp.status();
                tracing::info!("raw response from fastly: {:#?}", kv_resp.text().await?);
                tracing::warn!("fastly returned bad error code {}", status);
            }
        }

        let cdn_url_parsed = Url::parse(config.cdn_url.as_str())?;
        let cdn_host = cdn_url_parsed.host_str().unwrap_or(config.cdn_url.as_str());

        let cache_resp = client
            .post(format!(
                "https://api.fastly.com/purge/{}/{}/{}.{}",
                cdn_host, system_uuid, image_id, extension
            ))
            .header("Fastly-Key", config.fastly_token.as_ref().unwrap())
            .send()
            .await?;

        match cache_resp.status() {
            StatusCode::OK => {
                info!(
                    "successfully purged image {}/{}.{} from fastly cache",
                    system_uuid, image_id, extension
                );
            }
            _ => {
                let status = cache_resp.status();
                tracing::info!("raw response from fastly: {:#?}", cache_resp.text().await?);
                tracing::warn!("fastly returned bad error code {}", status);
            }
        }
    }

    sqlx::query("delete from images_assets where id = $1 and system_uuid = $2")
        .bind(image_id.clone())
        .bind(system_uuid.clone())
        .execute(&mut *tx)
        .await?;

    tx.commit().await?;

    Ok(())
}

#[derive(sqlx::FromRow)]
struct HashCleanupJobEntry {
    hash: String,
}

async fn cleanup_hash_job(pool: sqlx::PgPool, bucket: Arc<s3::Bucket>) -> anyhow::Result<()> {
    let mut tx = pool.begin().await?;

    let config = libpk::config
        .avatars
        .as_ref()
        .expect("missing avatar service config");

    let entry: Option<HashCleanupJobEntry> = sqlx::query_as(
        // no timestamp checking here
        // images are only added to the table after 24h
        r#"
                select hash from image_hash_cleanup_jobs
                for update skip locked limit 1;"#,
    )
    .fetch_optional(&mut *tx)
    .await?;
    if entry.is_none() {
        info!("no hash job to run, sleeping for 1 minute");
        tokio::time::sleep(tokio::time::Duration::from_secs(60)).await;
        return Ok(());
    }
    let entry = entry.unwrap();
    let hash = entry.hash;
    info!("got orphaned hash {hash}, cleaning up...");

    let url: Option<String> = sqlx::query_scalar("select url from images_hashes where hash = $1")
        .bind(&hash)
        .fetch_optional(&mut *tx)
        .await?;

    if let Some(url) = url {
        let path = url.strip_prefix(config.cdn_url.as_str()).unwrap();
        let s3_resp = bucket.delete_object(path).await?;
        match s3_resp.status_code() {
            204 => {
                info!("successfully deleted image {hash} from s3");
            }
            _ => {
                anyhow::bail!("s3 returned bad error code {}", s3_resp.status_code());
            }
        }
    }

    sqlx::query("delete from images_hashes where hash = $1")
        .bind(&hash)
        .execute(&mut *tx)
        .await?;

    tx.commit().await?;

    Ok(())
}
