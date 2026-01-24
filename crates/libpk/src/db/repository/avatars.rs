use sqlx::{PgPool, Postgres, Transaction};
use uuid::Uuid;

use crate::db::types::avatars::*;

pub async fn get_by_id(
    pool: &PgPool,
    system_uuid: Uuid,
    id: Uuid,
) -> anyhow::Result<Option<Image>> {
    Ok(sqlx::query_as(
        "select * from images_assets a join images_hashes h ON a.image = h.hash where id = $1 and system_uuid = $2",
    )
    .bind(id)
    .bind(system_uuid)
    .fetch_optional(pool)
    .await?)
}

pub async fn get_by_system(pool: &PgPool, system_uuid: Uuid) -> anyhow::Result<Vec<Image>> {
    Ok(sqlx::query_as(
        "select * from images_assets a join images_hashes h ON a.image = h.hash where system_uuid = $1",
    )
    .bind(system_uuid)
    .fetch_all(pool)
    .await?)
}

pub async fn get_full_by_hash(
    pool: &PgPool,
    system_uuid: Uuid,
    image_hash: String,
) -> anyhow::Result<Option<Image>> {
    Ok(sqlx::query_as(
        "select * from images_assets a join images_hashes h ON a.image = h.hash where system_uuid = $1 and h.hash = $2",
    )
    .bind(system_uuid)
    .bind(image_hash)
    .fetch_optional(pool)
    .await?)
}

pub async fn get_by_hash(pool: &PgPool, image_hash: String) -> anyhow::Result<Option<ImageData>> {
    Ok(
        sqlx::query_as("select * from images_hashes where hash = $1")
            .bind(image_hash)
            .fetch_optional(pool)
            .await?,
    )
}

pub async fn get_by_original_url(
    pool: &PgPool,
    original_url: &str,
) -> anyhow::Result<Option<Image>> {
    Ok(
        sqlx::query_as("select * from images_assets a join images_hashes h ON a.image = h.hash where original_url = $1")
            .bind(original_url)
            .fetch_optional(pool)
            .await?,
    )
}

pub async fn get_by_attachment_id(
    pool: &PgPool,
    attachment_id: u64,
) -> anyhow::Result<Option<Image>> {
    Ok(
        sqlx::query_as("select * from images_assets a join images_hashes h ON a.image = h.hash where original_attachment_id = $1")
            .bind(attachment_id as i64)
            .fetch_optional(pool)
            .await?,
    )
}

pub async fn remove_deletion_queue(pool: &PgPool, attachment_id: u64) -> anyhow::Result<()> {
    sqlx::query(
        r#"
            delete from image_cleanup_jobs
            where id in (
                select id from images
                where original_attachment_id = $1
            )
        "#,
    )
    .bind(attachment_id as i64)
    .execute(pool)
    .await?;

    Ok(())
}

pub async fn pop_queue(
    pool: &PgPool,
) -> anyhow::Result<Option<(Transaction<'_, Postgres>, ImageQueueEntry)>> {
    let mut tx = pool.begin().await?;
    let res: Option<ImageQueueEntry> = sqlx::query_as("delete from image_queue where itemid = (select itemid from image_queue order by itemid for update skip locked limit 1) returning *")
        .fetch_optional(&mut *tx).await?;
    Ok(res.map(|x| (tx, x)))
}

pub async fn get_queue_length(pool: &PgPool) -> anyhow::Result<i64> {
    Ok(sqlx::query_scalar("select count(*) from image_queue")
        .fetch_one(pool)
        .await?)
}

pub async fn get_stats(pool: &PgPool) -> anyhow::Result<Stats> {
    Ok(sqlx::query_as(
        "select count(*) as total_images, sum(file_size) as total_file_size from images",
    )
    .fetch_one(pool)
    .await?)
}

pub async fn add_image(pool: &PgPool, image: Image) -> anyhow::Result<ImageResult> {
    let kind_str = image.meta.kind.to_string();

    add_image_data(pool, &image.data).await?;

    if let Some(img) = get_full_by_hash(pool, image.meta.system_uuid, image.meta.image).await? {
        return Ok(ImageResult {
            is_new: false,
            uuid: img.meta.id,
        });
    }

    let res: (uuid::Uuid,) = sqlx::query_as(
        "insert into images_assets (system_uuid, image, proxy_image, kind, original_url, original_file_size, original_type, original_attachment_id, uploaded_by_account) 
     values ($1, $2, $3, $4, $5, $6, $7, $8, $9)
     returning id"
    )
    .bind(image.meta.system_uuid)
    .bind(image.data.hash)
    .bind (image.meta.proxy_image)
    .bind(kind_str)
    .bind(image.meta.original_url)
    .bind(image.meta.original_file_size)
    .bind(image.meta.original_type)
    .bind(image.meta.original_attachment_id)
    .bind(image.meta.uploaded_by_account)
    .fetch_one(pool)
    .await?;

    Ok(ImageResult {
        is_new: true,
        uuid: res.0,
    })
}

pub async fn add_image_data(pool: &PgPool, image_data: &ImageData) -> anyhow::Result<()> {
    sqlx::query(
        "insert into images_hashes (hash, url, file_size, width, height, content_type) 
     values ($1, $2, $3, $4, $5, $6) 
     on conflict (hash) do nothing",
    )
    .bind(&image_data.hash)
    .bind(&image_data.url)
    .bind(image_data.file_size)
    .bind(image_data.width)
    .bind(image_data.height)
    .bind(&image_data.content_type)
    .execute(pool)
    .await?;
    return Ok(());
}

pub async fn push_queue(
    conn: &mut sqlx::PgConnection,
    url: &str,
    kind: ImageKind,
) -> anyhow::Result<()> {
    sqlx::query("insert into image_queue (url, kind) values ($1, $2)")
        .bind(url)
        .bind(kind)
        .execute(conn)
        .await?;
    Ok(())
}
