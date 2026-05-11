use libpk::db::repository::avatars::IMAGE_CHECK_COLUMNS;
use pluralkit_models::SystemId;
use tracing::{error, info};
use uuid::Uuid;

#[libpk::main]
async fn main() -> anyhow::Result<()> {
    let config = libpk::config.avatars();

    let s3_client = libpk::s3::create_client(&config.s3);

    let pool = libpk::db::init_data_db().await?;

    loop {
        // no infinite loops
        // todo(premium): probably should just be separate threads
        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
        match cleanup_job(pool.clone()).await {
            Ok(()) => {}
            Err(error) => {
                error!(?error, "failed to run avatar cleanup job");
                // sentry
            }
        }
        match cleanup_hash_job(
            pool.clone(),
            s3_client.clone(),
            config.storage_bucket.clone(),
        )
        .await
        {
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
    system_id: SystemId,
}

async fn cleanup_job(pool: sqlx::PgPool) -> anyhow::Result<()> {
    let mut tx = pool.begin().await?;

    let entry: Option<CleanupJobEntry> = sqlx::query_as(
        r#"
                select id, system_id from image_cleanup_jobs
                where ts < now() - '24 hours'::interval
                for update skip locked limit 1;"#,
    )
    .fetch_optional(&mut *tx)
    .await?;
    if entry.is_none() {
        tx.rollback().await?;
        info!("no job to run, sleeping for 1 minute");
        tokio::time::sleep(tokio::time::Duration::from_secs(60)).await;
        return Ok(());
    }
    let entry = entry.unwrap();
    let image_id = entry.id;
    let system_id = entry.system_id;
    info!("got image {image_id}, cleaning up...");

    let image = libpk::db::repository::avatars::get_by_id(&pool, system_id, image_id).await?;
    if image.is_none() {
        info!("image {image_id} was already deleted, skipping");
        sqlx::query("delete from image_cleanup_jobs where id = $1 and system_id = $2")
            .bind(image_id)
            .bind(system_id)
            .execute(&mut *tx)
            .await?;
        tx.commit().await?;
        return Ok(());
    }
    let image = image.unwrap();

    // check if image is still in use
    let config = libpk::config.avatars();
    let system_uuid: uuid::Uuid = sqlx::query_scalar("select uuid from systems where id = $1")
        .bind(system_id)
        .fetch_one(&mut *tx)
        .await?;
    let ext = image
        .data
        .url
        .rsplit_once('.')
        .map(|(_, ext)| ext)
        .unwrap_or("");

    let asset_url = format!(
        "https://{}/images/{}/{}.{}",
        config.cdn_url, system_uuid, image_id, ext
    );

    let mut q = String::from("select 1 where false");
    for (table, col) in IMAGE_CHECK_COLUMNS.iter() {
        q.push_str(&format!(
            "\nunion all select 1 from {table} where {col} = $1"
        ));
    }
    q.push_str("\nlimit 1");

    let in_use = sqlx::query_scalar::<_, i32>(&q)
        .bind(&asset_url)
        .fetch_optional(&mut *tx)
        .await?
        .is_some();

    if in_use {
        info!("image {image_id} is found in use again, removing from cleanup queue");
        sqlx::query("delete from image_cleanup_jobs where id = $1 and system_id = $2")
            .bind(image_id)
            .bind(system_id)
            .execute(&mut *tx)
            .await?;
        tx.commit().await?;
        return Ok(());
    }

    sqlx::query("update images_assets set deleted_at = now() where id = $1 and system_id = $2")
        .bind(image_id)
        .bind(system_id)
        .execute(&mut *tx)
        .await?;

    sqlx::query("delete from image_cleanup_jobs where id = $1 and system_id = $2")
        .bind(image_id)
        .bind(system_id)
        .execute(&mut *tx)
        .await?;

    tx.commit().await?;

    Ok(())
}

#[derive(sqlx::FromRow)]
struct HashCleanupJobEntry {
    hash: String,
}

async fn cleanup_hash_job(
    pool: sqlx::PgPool,
    s3_client: aws_sdk_s3::Client,
    s3_bucket: String,
) -> anyhow::Result<()> {
    let mut tx = pool.begin().await?;

    let config = libpk::config.avatars();

    let entry: Option<HashCleanupJobEntry> = sqlx::query_as(
        r#"
                select hash from image_hash_cleanup_jobs
                where ts < now() - '24 hours'::interval
                for update skip locked limit 1;"#,
    )
    .fetch_optional(&mut *tx)
    .await?;
    if entry.is_none() {
        tx.rollback().await?;
        info!("no hash job to run, sleeping for 1 minute");
        tokio::time::sleep(tokio::time::Duration::from_secs(60)).await;
        return Ok(());
    }
    let entry = entry.unwrap();
    let hash = entry.hash;
    info!("got orphaned hash {hash}, cleaning up...");

    // re-check that no active images_assets row references this hash
    let still_referenced: Option<i32> = sqlx::query_scalar(
        "select 1 from images_assets where (image = $1 or proxy_image = $1) and deleted_at is null limit 1",
    )
    .bind(&hash)
    .fetch_optional(&mut *tx)
    .await?;

    if still_referenced.is_some() {
        info!("hash {hash} is still referenced by an asset, removing from cleanup queue");
        sqlx::query("delete from image_hash_cleanup_jobs where hash = $1")
            .bind(&hash)
            .execute(&mut *tx)
            .await?;
        tx.commit().await?;
        return Ok(());
    }

    // also check entity columns against both url formats:
    // - legacy: column = h.url
    // - new: column = asset-format url of any (possibly soft-deleted) asset that uses this hash
    let url: String = sqlx::query_scalar("select url from images_hashes where hash = $1")
        .bind(&hash)
        .fetch_one(&mut *tx)
        .await?;

    let cdn_prefix = format!("https://{}/images/", config.cdn_url);

    let mut q = String::from("select 1 where false");
    for (table, col) in IMAGE_CHECK_COLUMNS.iter() {
        q.push_str(&format!(
            r#"
            union all
            select 1 from {table}
            where {col} = $1
            or {col} in (
                select $2 || s.uuid::text || '/' || a.id::text || '.' || substring($1 from '\.([^.]+)$')
                from images_assets a
                join systems s on s.id = a.system_id
                where a.image = $3 or a.proxy_image = $3
            )
            "#
        ));
    }
    q.push_str("\nlimit 1");

    let in_use = sqlx::query_scalar::<_, i32>(&q)
        .bind(&url)
        .bind(&cdn_prefix)
        .bind(&hash)
        .fetch_optional(&mut *tx)
        .await?
        .is_some();

    if in_use {
        info!("direct hash {hash} is still in use by an entity, removing from cleanup queue");
        sqlx::query("delete from image_hash_cleanup_jobs where hash = $1")
            .bind(&hash)
            .execute(&mut *tx)
            .await?;
        tx.commit().await?;
        return Ok(());
    }

    // delete from db first (cascades the cleanup queue row via FK), then s3.
    // if s3 fails we leave an orphan in s3, but the db is consistent and we
    // can recover from that out-of-band.
    sqlx::query("delete from images_hashes where hash = $1")
        .bind(&hash)
        .execute(&mut *tx)
        .await?;

    tx.commit().await?;

    let prefix = format!("https://{}/", config.cdn_url);
    let Some(path) = url.strip_prefix(&prefix) else {
        error!(%url, %prefix, "hash url does not start with expected cdn prefix; skipping s3 delete");
        return Ok(());
    };
    match s3_client
        .delete_object()
        .bucket(&s3_bucket)
        .key(path)
        .send()
        .await
    {
        Ok(_) => info!("successfully deleted image {hash} from s3"),
        Err(err) => {
            error!(?err, %hash, "failed to delete image from s3 (orphaned in s3, db is clean)")
        }
    }

    Ok(())
}
