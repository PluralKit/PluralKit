mod hash;
// mod migrate;
mod process;
mod pull;
mod store;

use anyhow::Context;
use axum::extract::{DefaultBodyLimit, Multipart, State};
use axum::http::HeaderMap;
use axum::routing::get;
use axum::{
    Json, Router,
    http::StatusCode,
    response::{IntoResponse, Response},
    routing::post,
};
use libpk::_config::AvatarsConfig;
use libpk::db::repository::avatars as db;
use libpk::db::types::avatars::*;
use pull::ParsedUrl;
use reqwest::{Client, ClientBuilder};
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use std::error::Error;
use std::net::IpAddr;
use std::str::FromStr;
use std::sync::Arc;
use std::time::Duration;
use thiserror::Error;
use tracing::{error, info, warn};
use uuid::Uuid;

const NORMAL_HARD_LIMIT: usize = 8 * 1024 * 1024;
const PREMIUM_SOFT_LIMIT: usize = 30 * 1024 * 1024;
const PREMIUM_HARD_LIMIT: usize = 50 * 1024 * 1024;

#[derive(Error, Debug)]
pub enum PKAvatarError {
    // todo: split off into logical groups (cdn/url error, image format error, etc)
    #[error("invalid cdn url")]
    InvalidCdnUrl,

    #[error("discord cdn responded with status code: {0}")]
    BadCdnResponse(reqwest::StatusCode),

    #[error("server responded with status code: {0}")]
    BadServerResponse(reqwest::StatusCode),

    #[error("network error: {0}")]
    NetworkError(reqwest::Error),

    #[error("network error: {0}")]
    NetworkErrorString(String),

    #[error("response is missing header: {0}")]
    MissingHeader(&'static str),

    #[error("unsupported content type: {0}")]
    UnsupportedContentType(String),

    #[error("image file size too large ({0} > {1})")]
    ImageFileSizeTooLarge(u64, u64),

    #[error("unsupported image format: {0:?}")]
    UnsupportedImageFormat(image::ImageFormat),

    #[error("could not detect image format")]
    UnknownImageFormat,

    #[error("original image dimensions too large: {0:?} > {1:?}")]
    ImageDimensionsTooLarge((u32, u32), (u32, u32)),

    #[error("could not decode image, is it corrupted?")]
    ImageFormatError(#[from] image::ImageError),

    #[error("unknown error")]
    InternalError(#[from] anyhow::Error),
}

#[derive(Deserialize, Debug)]
pub struct PullRequest {
    url: String,
    kind: ImageKind,
    uploaded_by: Option<u64>, // should be String? serde makes this hard :/
    system_id: Option<Uuid>,

    #[serde(default)]
    force: bool,
}

#[derive(Serialize)]
pub struct ImageResponse {
    url: String,
    new: bool,
}

async fn gen_proxy_image(state: &AppState, data: Vec<u8>) -> Result<Option<String>, PKAvatarError> {
    let encoded_proxy = process::process_async(data, ImageKind::Avatar).await?;
    let store_proxy_res = crate::store::store(&state.bucket, &encoded_proxy).await?;
    let proxy_url = format!("{}{}", state.config.cdn_url, store_proxy_res.path);
    db::add_image_data(
        &state.pool,
        &ImageData {
            hash: encoded_proxy.hash.to_string(),
            url: proxy_url,
            file_size: encoded_proxy.data.len() as i32,
            width: encoded_proxy.width as i32,
            height: encoded_proxy.height as i32,
            content_type: encoded_proxy.format.to_mime_type().to_string(),
            created_at: None,
        },
    )
    .await?;
    Ok(Some(encoded_proxy.hash.to_string()))
}

async fn handle_image(
    state: &AppState,
    data: Vec<u8>,
    mut meta: ImageMeta,
) -> Result<ImageResponse, PKAvatarError> {
    let original_file_size = data.len();
    let system_uuid = meta.system_uuid;

    if meta.kind.is_premium() && original_file_size > NORMAL_HARD_LIMIT {
        meta.proxy_image = gen_proxy_image(&state, data.clone()).await?;
    }

    let encoded = process::process_async(data, meta.kind).await?;
    let store_res = crate::store::store(&state.bucket, &encoded).await?;
    meta.image = store_res.id.clone();
    let storage_url = format!("{}{}", state.config.cdn_url, store_res.path);

    let res = db::add_image(
        &state.pool,
        Image {
            meta: meta,
            data: ImageData {
                hash: store_res.id,
                url: storage_url,
                file_size: encoded.data.len() as i32,
                width: encoded.width as i32,
                height: encoded.height as i32,
                content_type: encoded.format.to_mime_type().to_string(),
                created_at: None,
            },
        },
    )
    .await?;

    if original_file_size >= PREMIUM_SOFT_LIMIT {
        warn!(
            "large image {} of size {} uploaded",
            res.uuid, original_file_size
        )
    }

    let final_url = format!(
        "{}images/{}/{}.{}",
        state.config.edge_url,
        system_uuid,
        res.uuid,
        encoded
            .format
            .extensions_str()
            .first()
            .expect("expected valid extension")
    );

    Ok(ImageResponse {
        url: final_url,
        new: res.is_new,
    })
}

async fn pull(
    State(state): State<AppState>,
    Json(req): Json<PullRequest>,
) -> Result<Json<ImageResponse>, PKAvatarError> {
    let parsed = pull::parse_url(&req.url) // parsing beforehand to "normalize"
        .map_err(|_| PKAvatarError::InvalidCdnUrl)?;
    if !(req.force || req.url.contains("https://serve.apparyllis.com/")) {
        if let Some(existing) = db::get_by_attachment_id(&state.pool, parsed.attachment_id).await? {
            // remove any pending image cleanup
            db::remove_deletion_queue(&state.pool, parsed.attachment_id).await?;
            return Ok(Json(ImageResponse {
                url: existing.data.url,
                new: false,
            }));
        }
    }
    let result = crate::pull::pull(&state.pull_client, &parsed, req.kind.is_premium()).await?;
    let original_file_size = result.data.len();

    Ok(Json(
        handle_image(
            &state,
            result.data,
            ImageMeta {
                id: Uuid::default(),
                system_uuid: req.system_id.expect("expected system id"),
                image: "".to_string(),
                proxy_image: None,
                kind: req.kind,
                original_url: Some(parsed.full_url),
                original_file_size: Some(original_file_size as i32),
                original_type: Some(result.content_type),
                original_attachment_id: Some(parsed.attachment_id as i64),
                uploaded_by_account: req.uploaded_by.map(|x| x as i64),
                uploaded_by_ip: None,
                uploaded_at: None,
            },
        )
        .await?,
    ))
}

async fn verify(
    State(state): State<AppState>,
    Json(req): Json<PullRequest>,
) -> Result<(), PKAvatarError> {
    let result = crate::pull::pull(
        &state.pull_client,
        &ParsedUrl {
            full_url: req.url.clone(),
            channel_id: 0,
            attachment_id: 0,
            filename: "".to_string(),
        },
        req.kind.is_premium(),
    )
    .await?;

    process::process_async(result.data, req.kind).await?;

    Ok(())
}

async fn upload(
    State(state): State<AppState>,
    headers: HeaderMap,
    mut multipart: Multipart,
) -> Result<Json<ImageResponse>, PKAvatarError> {
    let mut data: Option<Vec<u8>> = None;
    let mut kind: Option<ImageKind> = None;
    let mut system_id: Option<Uuid> = None;
    let mut upload_ip: Option<IpAddr> = None;

    if let Some(val) = headers.get("x-pluralkit-systemuuid")
        && let Ok(s) = val.to_str()
    {
        system_id = Uuid::parse_str(s).ok();
    }
    if let Some(val) = headers.get("x-pluralkit-client-ip")
        && let Ok(s) = val.to_str()
    {
        upload_ip = IpAddr::from_str(s).ok();
    }

    while let Some(field) = multipart
        .next_field()
        .await
        .map_err(|e| PKAvatarError::InternalError(e.into()))?
    {
        let name = field.name().unwrap_or("").to_string();

        match name.as_str() {
            "file" => {
                let bytes = field
                    .bytes()
                    .await
                    .map_err(|e| PKAvatarError::InternalError(e.into()))?;
                data = Some(bytes.to_vec());
            }
            "kind" => {
                let txt = field
                    .text()
                    .await
                    .map_err(|e| PKAvatarError::InternalError(e.into()))?;
                kind = ImageKind::from_string(&txt);
            }
            _ => {}
        }
    }

    let data = data.ok_or(PKAvatarError::MissingHeader("file"))?;
    let kind = kind.ok_or(PKAvatarError::MissingHeader("kind"))?;
    let system_id = system_id.ok_or(PKAvatarError::MissingHeader("x-pluralkit-systemuuid"))?;
    let upload_ip = upload_ip.ok_or(PKAvatarError::MissingHeader("x-pluralkit-client-ip"))?;

    Ok(Json(
        handle_image(
            &state,
            data,
            ImageMeta {
                id: Uuid::default(),
                system_uuid: system_id,
                image: "".to_string(),
                proxy_image: None,
                kind: kind,
                original_url: None,
                original_file_size: None,
                original_type: None,
                original_attachment_id: None,
                uploaded_by_account: None,
                uploaded_by_ip: Some(upload_ip),
                uploaded_at: None,
            },
        )
        .await?,
    ))
}

pub async fn stats(State(state): State<AppState>) -> Result<Json<Stats>, PKAvatarError> {
    Ok(Json(db::get_stats(&state.pool).await?))
}

#[derive(Clone)]
pub struct AppState {
    bucket: Arc<s3::Bucket>,
    pull_client: Arc<Client>,
    pool: PgPool,
    config: Arc<AvatarsConfig>,
}

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

    let pull_client = Arc::new(
        ClientBuilder::new()
            .connect_timeout(Duration::from_secs(3))
            .timeout(Duration::from_secs(3))
            .user_agent("PluralKit-Avatars/0.1")
            .build()
            .context("error making client")?,
    );

    let pool = libpk::db::init_data_db().await?;

    let state = AppState {
        bucket,
        pull_client,
        pool,
        config: Arc::new(config.clone()),
    };

    // migrations are done, disable this
    // migrate::spawn_migrate_workers(Arc::new(state.clone()), state.config.migrate_worker_count);

    let app = Router::new()
        .route("/verify", post(verify))
        .route("/pull", post(pull))
        .route("/upload", post(upload))
        .route("/stats", get(stats))
        .layer(DefaultBodyLimit::max(PREMIUM_HARD_LIMIT))
        .with_state(state);

    let host = &config.bind_addr;
    info!("starting server on {}!", host);
    let listener = tokio::net::TcpListener::bind(host).await.unwrap();
    axum::serve(listener, app).await.unwrap();

    Ok(())
}

#[derive(Serialize)]
struct ErrorResponse {
    error: String,
}

impl IntoResponse for PKAvatarError {
    fn into_response(self) -> Response {
        let status_code = match self {
            PKAvatarError::InternalError(_) | PKAvatarError::NetworkError(_) => {
                StatusCode::INTERNAL_SERVER_ERROR
            }
            _ => StatusCode::BAD_REQUEST,
        };

        // print inner error if otherwise hidden
        // `error!` calls go to sentry, so only use that if it's our error
        if matches!(self, PKAvatarError::InternalError(_)) {
            error!("error: {}", self.source().unwrap_or(&self));
        } else {
            warn!("error: {}", self.source().unwrap_or(&self));
        }

        (
            status_code,
            Json(ErrorResponse {
                error: self.to_string(),
            }),
        )
            .into_response()
    }
}
