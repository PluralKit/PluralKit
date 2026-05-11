// mod migrate;
mod process;
mod pull;
mod store;

use anyhow::Context;
use axum::extract::{DefaultBodyLimit, Request, State};
use axum::{
    Json, Router,
    http::StatusCode,
    middleware::Next,
    response::{IntoResponse, Response},
    routing::post,
};
use libpk::_config::AvatarsConfig;
use libpk::db::types::avatars::*;
use pull::ParsedUrl;
use reqwest::{Client, ClientBuilder};
use serde::{Deserialize, Serialize};
use std::error::Error;
use std::sync::Arc;
use std::time::Duration;
use subtle::ConstantTimeEq;
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

    #[serde(default)]
    force: bool,
}

#[derive(Serialize)]
pub struct ImageInfo {
    pub key: String,
    pub width: u32,
    pub height: u32,
    pub content_type: String,
    pub file_size: usize,
}

#[derive(Serialize)]
pub struct OriginalInfo {
    pub url: String,
    pub content_type: String,
    pub file_size: usize,
}

#[derive(Serialize)]
pub struct PullResponse {
    #[serde(flatten)]
    pub image: ImageInfo,

    #[serde(skip_serializing_if = "Option::is_none")]
    pub proxy: Option<ImageInfo>,

    pub original: OriginalInfo,
}

async fn pull(
    State(state): State<AppState>,
    Json(req): Json<PullRequest>,
) -> Result<Json<PullResponse>, PKAvatarError> {
    let parsed = pull::parse_url(&req.url).map_err(|_| PKAvatarError::InvalidCdnUrl)?;

    let result = crate::pull::pull(&state.pull_client, &parsed, req.kind.is_premium()).await?;
    let original_file_size = result.data.len();
    let original_content_type = result.content_type.clone();

    // generate proxy image if file size is above hard limit
    // todo(premium): this is probably not right and we should always generate a proxy image
    let proxy = if req.kind.is_premium() && original_file_size > NORMAL_HARD_LIMIT {
        let proxy_encoded = process::process_async(result.data.clone(), ImageKind::Avatar).await?;
        let proxy_uuid = Uuid::new_v4().to_string();
        let proxy_store_res = crate::store::store(
            &state.s3_client,
            &state.s3_bucket,
            &proxy_uuid,
            &proxy_encoded,
        )
        .await?;

        Some(ImageInfo {
            key: proxy_store_res.key,
            width: proxy_encoded.width,
            height: proxy_encoded.height,
            content_type: proxy_encoded.format.to_mime_type().to_string(),
            file_size: proxy_encoded.data.len(),
        })
    } else {
        None
    };

    let encoded = process::process_async(result.data, req.kind).await?;
    let uuid_key = Uuid::new_v4().to_string();
    let store_res =
        crate::store::store(&state.s3_client, &state.s3_bucket, &uuid_key, &encoded).await?;

    if original_file_size >= PREMIUM_SOFT_LIMIT {
        // todo(premium): log some more information here
        // or maybe just also log it in api
        warn!("large image of size {} uploaded", original_file_size);
    }

    Ok(Json(PullResponse {
        image: ImageInfo {
            key: store_res.key,
            width: encoded.width,
            height: encoded.height,
            content_type: encoded.format.to_mime_type().to_string(),
            file_size: encoded.data.len(),
        },
        proxy,
        original: OriginalInfo {
            url: parsed.full_url,
            content_type: original_content_type,
            file_size: original_file_size,
        },
    }))
}

async fn check_internal_auth(req: Request, next: Next) -> Response {
    let Some(ref expected) = libpk::config.internal_auth else {
        return next.run(req).await;
    };

    let authorized = req
        .headers()
        .get("x-pluralkit-internalauth")
        .and_then(|h| h.to_str().ok())
        .map(|h| h.as_bytes().ct_eq(expected.as_bytes()).into())
        .unwrap_or(false);

    if !authorized {
        return (StatusCode::UNAUTHORIZED, "unauthorized").into_response();
    }

    next.run(req).await
}

// todo: this endpoint is converting images and then doing nothing with the result
// that is a waste of resources
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
        false,
    )
    .await?;

    process::process_async(result.data, req.kind).await?;

    Ok(())
}

#[derive(Clone)]
pub struct AppState {
    s3_client: aws_sdk_s3::Client,
    s3_bucket: String,
    pull_client: Arc<Client>,
    config: Arc<AvatarsConfig>,
}

#[libpk::main]
async fn main() -> anyhow::Result<()> {
    let config = libpk::config.avatars();

    let s3_client = libpk::s3::create_client(&config.s3);

    let pull_client = Arc::new(
        ClientBuilder::new()
            .connect_timeout(Duration::from_secs(3))
            .timeout(Duration::from_secs(3))
            .user_agent("PluralKit-Avatars/0.1")
            .build()
            .context("error making client")?,
    );

    let state = AppState {
        s3_client,
        s3_bucket: config.uploads_bucket.clone(),
        pull_client,
        config: Arc::new(config.clone()),
    };

    let app = Router::new()
        .route("/verify", post(verify))
        .route("/pull", post(pull))
        .layer(DefaultBodyLimit::max(PREMIUM_HARD_LIMIT))
        .layer(axum::middleware::from_fn(check_internal_auth))
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
