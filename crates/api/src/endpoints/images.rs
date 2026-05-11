use crate::ApiContext;
use crate::auth::AuthState;
use crate::error::{
    GENERIC_AUTH_ERROR, GENERIC_BAD_REQUEST, GENERIC_MISSING_PERMISSIONS, GENERIC_NOT_FOUND,
    GENERIC_SERVER_ERROR,
};
use crate::fail;
use crate::middleware::params::RequestAbout;
use axum::Extension;
use axum::extract::Path;
use axum::http::uri::Scheme;
use axum::response::IntoResponse;
use axum::{extract::State, response::Json};
use hyper::Uri;
use libpk::db::repository::avatars as avatars_db;
use libpk::db::types::avatars::*;
use libpk::hash::Hash;
use pk_macros::api_endpoint;
use pluralkit_models::SystemId;
use serde::{Deserialize, Serialize};
use sqlx::types::Uuid;
use std::result::Result::Ok;
use tracing::{info, warn};

#[derive(Serialize)]
struct APIImage {
    url: String,
    proxy_url: Option<String>,
}

#[derive(Deserialize)]
struct PullImageInfo {
    key: String,
    width: u32,
    height: u32,
    content_type: String,
    file_size: usize,
}

#[derive(Deserialize)]
struct PullOriginalInfo {
    url: String,
    content_type: String,
    file_size: usize,
}

#[derive(Deserialize)]
struct AvatarPullResponse {
    #[serde(flatten)]
    image: PullImageInfo,
    proxy: Option<PullImageInfo>,
    original: PullOriginalInfo,
}

#[derive(Deserialize)]
pub struct IngestRequest {
    url: Option<String>,
    kind: ImageKind,
}

#[derive(Serialize)]
pub struct IngestResponse {
    url: String,
    new: bool, // todo(premium): unused
}

fn ext_from_content_type(content_type: &str) -> &str {
    match content_type {
        "image/webp" => "webp",
        "image/png" => "png",
        "image/jpeg" => "jpg",
        "image/gif" => "gif",
        "image/tiff" => "tiff",
        _ => "bin",
    }
}

/// Read an S3 object, compute its hash, and return the hash string + final images/ path.
async fn hash_and_path(
    client: &aws_sdk_s3::Client,
    bucket: &str,
    key: &str,
    ext: &str,
) -> Result<(String, String), crate::error::PKError> {
    let resp = client
        .get_object()
        .bucket(bucket)
        .key(key)
        .send()
        .await
        .map_err(|e| {
            tracing::error!(?e, key, "failed to read image from S3");
            GENERIC_SERVER_ERROR
        })?;
    let data = resp.body.collect().await.map_err(|e| {
        tracing::error!(?e, key, "failed to read S3 response body");
        GENERIC_SERVER_ERROR
    })?;
    let hash = Hash::sha256(&data.into_bytes());
    let hash_str = hash.to_string();
    let final_path = format!("images/{}/{}.{}", &hash_str[..2], &hash_str[2..], ext);
    Ok((hash_str, final_path))
}

// do a CopyObject to storage bucket if not already exists
async fn copy_to_storage_if_new(
    client: &aws_sdk_s3::Client,
    src_bucket: &str,
    dst_bucket: &str,
    db: &sqlx::PgPool,
    src_key: &str,
    final_path: &str,
    hash_str: &str,
) -> Result<bool, crate::error::PKError> {
    let existing = avatars_db::get_by_hash(db, hash_str.to_string())
        .await
        .map_err(|e| {
            tracing::error!(?e, "failed to check image hash dedup");
            GENERIC_SERVER_ERROR
        })?;

    if existing.is_some() {
        return Ok(false);
    }

    client
        .copy_object()
        .bucket(dst_bucket)
        .copy_source(format!("{}/{}", src_bucket, src_key))
        .key(final_path)
        .send()
        .await
        .map_err(|e| {
            tracing::error!(?e, "failed to copy image to final S3 path");
            GENERIC_SERVER_ERROR
        })?;

    Ok(true)
}

// for write tx rollback
async fn try_delete_s3_object(client: &aws_sdk_s3::Client, bucket: &str, key: &str) {
    if let Err(e) = client.delete_object().bucket(bucket).key(key).send().await {
        tracing::error!(
            ?e,
            key,
            "failed to clean up orphaned S3 object after rollback"
        );
    }
}

async fn write_ingest_tx(
    pool: &sqlx::PgPool,
    main_data: &ImageData,
    proxy_data: Option<&ImageData>,
    image: ImageMeta,
) -> Result<ImageResult, crate::error::PKError> {
    let mut tx = pool.begin().await.map_err(|e| {
        tracing::error!(?e, "failed to begin ingest transaction");
        GENERIC_SERVER_ERROR
    })?;

    avatars_db::add_image_data(&mut *tx, main_data)
        .await
        .map_err(|e| {
            tracing::error!(?e, "failed to insert image hash data");
            GENERIC_SERVER_ERROR
        })?;

    if let Some(proxy) = proxy_data {
        avatars_db::add_image_data(&mut *tx, proxy)
            .await
            .map_err(|e| {
                tracing::error!(?e, "failed to insert proxy image hash data");
                GENERIC_SERVER_ERROR
            })?;
    }

    let res = avatars_db::add_image(&mut *tx, image).await.map_err(|e| {
        tracing::error!(?e, "failed to insert image asset row");
        GENERIC_SERVER_ERROR
    })?;

    tx.commit().await.map_err(|e| {
        tracing::error!(?e, "failed to commit ingest transaction");
        GENERIC_SERVER_ERROR
    })?;

    Ok(res)
}

// take an uploaded image and actually save it
async fn ingest_from_pull(
    ctx: &ApiContext,
    system_id: SystemId,
    system_uuid: Uuid,
    pull_resp: &AvatarPullResponse,
    kind: ImageKind,
    uploaded_by: Option<u64>,
    original_attachment_id: Option<i64>,
) -> Result<IngestResponse, crate::error::PKError> {
    let client = &ctx.s3_client;

    // copy main image to storage and prepare hash row
    let img = &pull_resp.image;
    let ext = ext_from_content_type(&img.content_type);
    let (hash_str, final_path) = hash_and_path(client, &ctx.uploads_bucket, &img.key, ext).await?;
    let storage_url = format!("https://{}/{}", libpk::config.api().cdn_url, final_path);

    let copied_main = copy_to_storage_if_new(
        client,
        &ctx.uploads_bucket,
        &ctx.storage_bucket,
        &ctx.db,
        &img.key,
        &final_path,
        &hash_str,
    )
    .await?;

    let main_image_data = ImageData {
        hash: hash_str.clone(),
        url: storage_url,
        file_size: img.file_size as i32,
        width: img.width as i32,
        height: img.height as i32,
        content_type: img.content_type.clone(),
        created_at: None,
    };

    // same for proxy image if present
    let (proxy_hash, proxy_image_data, copied_proxy_path) = if let Some(ref proxy) = pull_resp.proxy
    {
        let proxy_ext = ext_from_content_type(&proxy.content_type);
        let (proxy_hash_str, proxy_final_path) =
            hash_and_path(client, &ctx.uploads_bucket, &proxy.key, proxy_ext).await?;
        let proxy_storage_url = format!(
            "https://{}/{}",
            libpk::config.api().cdn_url,
            proxy_final_path
        );

        let copied = copy_to_storage_if_new(
            client,
            &ctx.uploads_bucket,
            &ctx.storage_bucket,
            &ctx.db,
            &proxy.key,
            &proxy_final_path,
            &proxy_hash_str,
        )
        .await?;

        let data = ImageData {
            hash: proxy_hash_str.clone(),
            url: proxy_storage_url,
            file_size: proxy.file_size as i32,
            width: proxy.width as i32,
            height: proxy.height as i32,
            content_type: proxy.content_type.clone(),
            created_at: None,
        };

        (
            Some(proxy_hash_str),
            Some(data),
            if copied { Some(proxy_final_path) } else { None },
        )
    } else {
        (None, None, None)
    };

    // save to db
    let image = ImageMeta {
        id: Uuid::default(),
        system_id,
        image: hash_str,
        proxy_image: proxy_hash,
        kind,
        original_url: Some(pull_resp.original.url.clone()),
        original_file_size: Some(pull_resp.original.file_size as i32),
        original_type: Some(pull_resp.original.content_type.clone()),
        original_attachment_id,
        uploaded_by_account: uploaded_by.map(|x| x as i64),
        uploaded_by_ip: None,
        uploaded_at: None,
        deleted_at: None,
    };

    let res =
        match write_ingest_tx(&ctx.db, &main_image_data, proxy_image_data.as_ref(), image).await {
            Ok(res) => res,
            Err(e) => {
                // best-effort: delete S3 objects we just copied, since the DB rolled back
                if copied_main {
                    try_delete_s3_object(client, &ctx.storage_bucket, &final_path).await;
                }
                if let Some(p) = copied_proxy_path {
                    try_delete_s3_object(client, &ctx.storage_bucket, &p).await;
                }
                return Err(e);
            }
        };

    // no need to delete uploads file here
    // it's not publicly accessible and it'll get TTLed automatically anyway

    let final_url = format!(
        "https://{}/images/{}/{}.{}",
        libpk::config.api().cdn_url,
        system_uuid,
        res.uuid,
        ext
    );

    info!(
        system_uuid = %system_uuid,
        image_uuid = %res.uuid,
        new = res.is_new,
        "ingested image"
    );

    Ok(IngestResponse {
        url: final_url,
        new: res.is_new,
    })
}

async fn call_avatar_pull(
    ctx: &ApiContext,
    url: String,
    kind: ImageKind,
) -> Result<AvatarPullResponse, crate::error::PKError> {
    let resp = ctx.avatar_service.pull(url, kind).await.map_err(|e| {
        tracing::error!(?e, "failed to call avatar service /pull");
        GENERIC_SERVER_ERROR
    })?;

    if !resp.status().is_success() {
        let status = resp.status();
        let body = resp.text().await.unwrap_or_default();
        tracing::error!(status = %status, body = body, "avatar service /pull returned error");
        return Err(GENERIC_BAD_REQUEST);
    }

    resp.json::<AvatarPullResponse>().await.map_err(|e| {
        tracing::error!(?e, "failed to parse avatar service /pull response");
        GENERIC_SERVER_ERROR
    })
}

// todo(premium): move this to its own binary
#[api_endpoint]
pub async fn image_data(
    State(ctx): State<ApiContext>,
    Path((first, second)): Path<(String, String)>,
) -> impl IntoResponse {
    let file_stem = second.split('.').next().unwrap_or(&second);

    let (s3_key, content_type_hint) = if let (Ok(system_uuid), Ok(image_uuid)) =
        (first.parse::<Uuid>(), file_stem.parse::<Uuid>())
    {
        // new format: /images/{system_uuid}/{image_uuid}.webp
        let img: Image =
            match avatars_db::get_by_id_and_system_uuid(&ctx.db, system_uuid, image_uuid).await {
                Ok(Some(img)) => img,
                Ok(None) => return Err(GENERIC_NOT_FOUND),
                Err(err) => fail!(?err, "failed to query image"),
            };

        let hash = &img.data.hash;
        let ext = ext_from_content_type(&img.data.content_type);
        (
            format!("images/{}/{}.{}", &hash[..2], &hash[2..], ext),
            img.data.content_type,
        )
    } else {
        // legacy format: /images/{hash[..2]}/{hash[2..]}.webp
        let hash = format!("{}{}", first, file_stem);
        let image_data = match avatars_db::get_by_hash(&ctx.db, hash).await {
            Ok(Some(data)) => data,
            Ok(None) => return Err(GENERIC_NOT_FOUND),
            Err(err) => fail!(?err, "failed to query image by hash"),
        };

        let ext = ext_from_content_type(&image_data.content_type);
        (
            format!("images/{}/{}.{}", &first, file_stem, ext),
            image_data.content_type,
        )
    };

    let resp = ctx
        .s3_client
        .get_object()
        .bucket(&ctx.storage_bucket)
        .key(&s3_key)
        .send()
        .await
        .map_err(|e| {
            tracing::error!(?e, s3_key, "failed to fetch image from S3");
            GENERIC_NOT_FOUND
        })?;

    let content_type = resp
        .content_type()
        .unwrap_or(&content_type_hint)
        .to_string();

    let body = resp.body.collect().await.map_err(|e| {
        tracing::error!(?e, s3_key, "failed to read S3 response body");
        GENERIC_SERVER_ERROR
    })?;

    Ok((
        [
            ("Content-Type", content_type),
            (
                "Cache-Control",
                "public, max-age=31536000, immutable".to_string(),
            ),
        ],
        body.into_bytes().to_vec(),
    ))
}

#[api_endpoint]
pub async fn list_images(
    Extension(auth): Extension<AuthState>,
    Extension(about): Extension<RequestAbout>,
    State(ctx): State<ApiContext>,
) -> Json<Vec<APIImage>> {
    if auth.system_id() != Some(about.system_id()) {
        return Err(GENERIC_MISSING_PERMISSIONS);
    }

    let system_id = about.system_id();
    let system_uuid: Uuid = match sqlx::query_scalar("select uuid from systems where id = $1")
        .bind(system_id)
        .fetch_optional(&ctx.db)
        .await
    {
        Ok(Some(uuid)) => uuid,
        Ok(None) => return Err(GENERIC_NOT_FOUND),
        Err(err) => fail!(?err, "failed to query system uuid"),
    };

    let images = match avatars_db::get_by_system(&ctx.db, system_id).await {
        Ok(images) => images,
        Err(err) => fail!(?err, "failed to query images"),
    };

    let result: Vec<APIImage> = images
        .into_iter()
        .map(|img| {
            let ext = ext_from_content_type(&img.data.content_type);
            APIImage {
                url: format!(
                    "https://{}/images/{}/{}.{}",
                    libpk::config.api().cdn_url,
                    system_uuid,
                    img.meta.id,
                    ext
                ),
                proxy_url: None,
            }
        })
        .collect();

    Ok(Json(result))
}

#[api_endpoint]
pub async fn ingest_image(
    Extension(auth): Extension<AuthState>,
    Extension(about): Extension<RequestAbout>,
    State(ctx): State<ApiContext>,
    Json(req): Json<IngestRequest>,
) -> Json<serde_json::Value> {
    if auth.system_id() != Some(about.system_id()) {
        return Err(GENERIC_AUTH_ERROR);
    }

    let system_id = about.system_id();
    let system_uuid: Uuid = match sqlx::query_scalar("select uuid from systems where id = $1")
        .bind(system_id)
        .fetch_optional(&ctx.db)
        .await
    {
        Ok(Some(uuid)) => uuid,
        Ok(None) => return Err(GENERIC_NOT_FOUND),
        Err(err) => fail!(?err, "failed to query system uuid"),
    };

    if auth.internal() {
        // bot: pull discord cdn url
        let url = Uri::try_from(req.url.as_deref().ok_or_else(|| GENERIC_BAD_REQUEST)?)
            .map_err(|_| GENERIC_BAD_REQUEST)?;
        if url.scheme() != Some(&Scheme::HTTPS) {
            warn!("bot request for url {:?} was not https", req.url);
            return Err(GENERIC_BAD_REQUEST);
        }
        if !matches!(url.host(), Some("cdn.discordapp.com"))
            && !matches!(url.host(), Some("media.discordapp.net"))
        {
            warn!(
                "bot request for url {:?} ({:?}) didn't seem to be in discord cdn",
                req.url,
                url.host()
            );
            return Err(GENERIC_BAD_REQUEST);
        }
        // /attachments/{channel_id}/{attachment_id}/{filename}.{ext}
        let attachment_id = url
            .path()
            .trim_start_matches('/')
            .split('/')
            .nth(2)
            .and_then(|s| s.parse::<u64>().ok());
        let Some(attachment_id) = attachment_id else {
            warn!(
                "bot request for url {:?} did not contain a parseable attachment id",
                req.url
            );
            return Err(GENERIC_BAD_REQUEST);
        };
        let pull_resp = call_avatar_pull(&ctx, url.to_string(), req.kind).await?;
        let result = ingest_from_pull(
            &ctx,
            system_id,
            system_uuid,
            &pull_resp,
            req.kind,
            None,
            Some(attachment_id as i64),
        )
        .await?;
        Ok(Json(serde_json::json!({
            "url": result.url,
        })))
    } else if let Some(url) = req.url {
        // dash: finalize upload
        // todo(premium)
        Ok(Json(serde_json::json!({
            "url": "none",
        })))
    } else if req.url.is_none() {
        // dash upload
        if !auth.is_premium() {
            return Err(crate::error::PREMIUM_REQUIRED);
        }
        // generate presigned PUT URL for direct upload to s3 bucket
        // slightly cursed: client needs to upload the image to cdn.pluralkit.me, but s3 backend
        // sees the bucket url as host
        // we can first create the signature for the bucket domain, then just change the host in the url
        // surprisingly: this works
        let client = &ctx.s3_client;
        let key = format!("uploads/{}.bin", Uuid::new_v4());
        let presigning_config = aws_sdk_s3::presigning::PresigningConfig::expires_in(
            std::time::Duration::from_secs(3600),
        )
        .map_err(|e| {
            tracing::error!(?e, "failed to create presigning config");
            GENERIC_SERVER_ERROR
        })?;
        let presigned_url = client
            .put_object()
            .bucket(&ctx.uploads_bucket)
            .key(&key)
            .presigned(presigning_config)
            .await
            .map_err(|e| {
                tracing::error!(?e, "failed to generate presigned PUT URL");
                GENERIC_SERVER_ERROR
            })?
            .uri()
            .to_string();

        info!(
            "generated presigned upload url for system {}: {}",
            auth.system_id().unwrap(),
            presigned_url
        );

        Ok(Json(serde_json::json!({
            "upload_url": presigned_url,
        })))
    } else {
        unreachable!()
    }
}
