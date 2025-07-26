use anyhow::Context;
use reqwest::{ClientBuilder, StatusCode};
use sqlx::prelude::FromRow;
use std::{sync::Arc, time::Duration};
use tracing::{error, info};

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
        match cleanup_job(pool.clone(), bucket.clone()).await {
            Ok(()) => {}
            Err(error) => {
                error!(?error, "failed to run avatar cleanup job");
                // sentry
            }
        }
    }
}

#[derive(FromRow)]
struct CleanupJobEntry {
    id: String,
}

async fn cleanup_job(pool: sqlx::PgPool, bucket: Arc<s3::Bucket>) -> anyhow::Result<()> {
    let mut tx = pool.begin().await?;

    let image_id: Option<CleanupJobEntry> = sqlx::query_as(
        // no timestamp checking here
        // images are only added to the table after 24h
        r#"
                select id from image_cleanup_jobs
                for update skip locked limit 1;"#,
    )
    .fetch_optional(&mut *tx)
    .await?;
    if image_id.is_none() {
        info!("no job to run, sleeping for 1 minute");
        tokio::time::sleep(tokio::time::Duration::from_secs(60)).await;
        return Ok(());
    }
    let image_id = image_id.unwrap().id;
    info!("got image {image_id}, cleaning up...");

    let image_data = libpk::db::repository::avatars::get_by_id(&pool, image_id.clone()).await?;
    if image_data.is_none() {
        // unsure how this can happen? there is a FK reference
        info!("image {image_id} was already deleted, skipping");
        sqlx::query("delete from image_cleanup_jobs where id = $1")
            .bind(image_id)
            .execute(&mut *tx)
            .await?;
        return Ok(());
    }
    let image_data = image_data.unwrap();

    let config = libpk::config
        .avatars
        .as_ref()
        .expect("missing avatar service config");

    let path = image_data
        .url
        .strip_prefix(config.cdn_url.as_str())
        .unwrap();

    let s3_resp = bucket.delete_object(path).await?;
    match s3_resp.status_code() {
        204 => {
            info!("successfully deleted image {image_id} from s3");
        }
        _ => {
            anyhow::bail!("s3 returned bad error code {}", s3_resp.status_code());
        }
    }

    if let Some(zone_id) = config.cloudflare_zone_id.as_ref() {
        let client = ClientBuilder::new()
            .connect_timeout(Duration::from_secs(3))
            .timeout(Duration::from_secs(3))
            .build()
            .context("error making client")?;

        let cf_resp = client
            .post(format!(
                "https://api.cloudflare.com/client/v4/zones/{zone_id}/purge_cache"
            ))
            .header(
                "Authorization",
                format!("Bearer {}", config.cloudflare_token.as_ref().unwrap()),
            )
            .body(format!(r#"{{"files":["{}"]}}"#, image_data.url))
            .send()
            .await?;

        match cf_resp.status() {
            StatusCode::OK => {
                info!(
                    "successfully purged url {} from cloudflare cache",
                    image_data.url
                );
            }
            _ => {
                let status = cf_resp.status();
                tracing::info!("raw response from cloudflare: {:#?}", cf_resp.text().await?);
                anyhow::bail!("cloudflare returned bad error code {}", status);
            }
        }
    }

    sqlx::query("delete from images where id = $1")
        .bind(image_id.clone())
        .execute(&mut *tx)
        .await?;

    tx.commit().await?;

    Ok(())
}
