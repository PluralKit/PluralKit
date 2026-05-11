use pluralkit_models::SystemId;
use sqlx::{PgPool, Postgres, Transaction};
use uuid::Uuid;

use crate::db::types::avatars::*;

pub const IMAGE_CHECK_COLUMNS: &[(&str, &str)] = &[
    ("systems", "avatar_url"),
    ("systems", "banner_image"),
    ("system_guild", "avatar_url"),
    ("members", "avatar_url"),
    ("members", "banner_image"),
    ("members", "webhook_avatar_url"),
    ("member_guild", "avatar_url"),
    ("groups", "icon"),
    ("groups", "banner_image"),
];

pub async fn get_by_id(
    pool: &PgPool,
    system_id: SystemId,
    id: Uuid,
) -> anyhow::Result<Option<Image>> {
    Ok(sqlx::query_as(
        "select a.*, h.* from images_assets a
         join images_hashes h on a.image = h.hash
         where a.id = $1 and a.system_id = $2 and a.deleted_at is null",
    )
    .bind(id)
    .bind(system_id)
    .fetch_optional(pool)
    .await?)
}

pub async fn get_by_id_and_system_uuid(
    pool: &PgPool,
    system_uuid: Uuid,
    id: Uuid,
) -> anyhow::Result<Option<Image>> {
    Ok(sqlx::query_as(
        "select a.*, h.* from images_assets a
         join images_hashes h on a.image = h.hash
         join systems s on s.id = a.system_id
         where a.id = $1 and s.uuid = $2 and a.deleted_at is null",
    )
    .bind(id)
    .bind(system_uuid)
    .fetch_optional(pool)
    .await?)
}

pub async fn get_by_system(pool: &PgPool, system_id: i32) -> anyhow::Result<Vec<Image>> {
    Ok(sqlx::query_as(
        "select * from images_assets a join images_hashes h ON a.image = h.hash where system_id = $1 and a.deleted_at is null",
    )
    .bind(system_id)
    .fetch_all(pool)
    .await?)
}

pub async fn get_full_by_hash(
    conn: &mut sqlx::PgConnection,
    system_id: SystemId,
    image_hash: String,
) -> anyhow::Result<Option<Image>> {
    Ok(sqlx::query_as(
        "select * from images_assets a join images_hashes h ON a.image = h.hash where system_id = $1 and h.hash = $2 and a.deleted_at is null",
    )
    .bind(system_id)
    .bind(image_hash)
    .fetch_optional(conn)
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

pub async fn add_image(
    conn: &mut sqlx::PgConnection,
    image: ImageMeta,
) -> anyhow::Result<ImageResult> {
    let kind_str = image.kind.to_string();

    if let Some(img) = get_full_by_hash(&mut *conn, image.system_id, image.image.clone()).await? {
        return Ok(ImageResult {
            is_new: false,
            uuid: img.meta.id,
        });
    }

    let res: (uuid::Uuid,) = sqlx::query_as(
        "insert into images_assets (system_id, image, proxy_image, kind, original_url, original_file_size, original_type, original_attachment_id, uploaded_by_account)
     values ($1, $2, $3, $4, $5, $6, $7, $8, $9)
     returning id"
    )
    .bind(image.system_id)
    .bind(image.image)
    .bind(image.proxy_image)
    .bind(kind_str)
    .bind(image.original_url)
    .bind(image.original_file_size)
    .bind(image.original_type)
    .bind(image.original_attachment_id)
    .bind(image.uploaded_by_account)
    .fetch_one(conn)
    .await?;

    Ok(ImageResult {
        is_new: true,
        uuid: res.0,
    })
}

pub async fn add_image_data(
    conn: &mut sqlx::PgConnection,
    image_data: &ImageData,
) -> anyhow::Result<()> {
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
    .execute(conn)
    .await?;
    return Ok(());
}
