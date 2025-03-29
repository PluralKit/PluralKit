mod hash;
// mod migrate;
mod process;
mod pull;
mod store;

use anyhow::Context;
use axum::extract::State;
use axum::routing::get;
use axum::{
    http::StatusCode,
    response::{IntoResponse, Response},
    routing::post,
    Json, Router,
};
use libpk::_config::AvatarsConfig;
use libpk::db::repository::avatars as db;
use libpk::db::types::avatars::*;
use pull::ParsedUrl;
use reqwest::{Client, ClientBuilder};
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use std::error::Error;
use std::sync::Arc;
use std::time::Duration;
use thiserror::Error;
use tracing::{error, info, warn};
use uuid::Uuid;

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
pub struct PullResponse {
    url: String,
    new: bool,
}

async fn pull(
    State(state): State<AppState>,
    Json(req): Json<PullRequest>,
) -> Result<Json<PullResponse>, PKAvatarError> {
    let parsed = pull::parse_url(&req.url) // parsing beforehand to "normalize"
        .map_err(|_| PKAvatarError::InvalidCdnUrl)?;
    if !req.force {
        if let Some(existing) = db::get_by_attachment_id(&state.pool, parsed.attachment_id).await? {
            // remove any pending image cleanup
            db::remove_deletion_queue(&state.pool, parsed.attachment_id).await?;
            return Ok(Json(PullResponse {
                url: existing.url,
                new: false,
            }));
        }
    }

    let result = crate::pull::pull(state.pull_client, &parsed).await?;

    let original_file_size = result.data.len();
    let encoded = process::process_async(result.data, req.kind).await?;

    let store_res = crate::store::store(&state.bucket, &encoded).await?;
    let final_url = format!("{}{}", state.config.cdn_url, store_res.path);
    let is_new = db::add_image(
        &state.pool,
        ImageMeta {
            id: store_res.id,
            url: final_url.clone(),
            content_type: encoded.format.mime_type().to_string(),
            original_url: Some(parsed.full_url),
            original_type: Some(result.content_type),
            original_file_size: Some(original_file_size as i32),
            original_attachment_id: Some(parsed.attachment_id as i64),
            file_size: encoded.data.len() as i32,
            width: encoded.width as i32,
            height: encoded.height as i32,
            kind: req.kind,
            uploaded_at: None,
            uploaded_by_account: req.uploaded_by.map(|x| x as i64),
            uploaded_by_system: req.system_id,
        },
    )
    .await?;

    Ok(Json(PullResponse {
        url: final_url,
        new: is_new,
    }))
}

async fn verify(
    State(state): State<AppState>,
    Json(req): Json<PullRequest>,
) -> Result<(), PKAvatarError> {
    let result = crate::pull::pull(
        state.pull_client,
        &ParsedUrl {
            full_url: req.url.clone(),
            channel_id: 0,
            attachment_id: 0,
            filename: "".to_string(),
        },
    )
    .await?;

    let encoded = process::process_async(result.data, req.kind).await?;

    Ok(())
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

libpk::main!("avatars");
async fn real_main() -> anyhow::Result<()> {
    let config = libpk::config
        .avatars
        .as_ref()
        .expect("missing avatar service config");

    let bucket = {
        let region = s3::Region::Custom {
            region: "s3".to_string(),
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
        .route("/stats", get(stats))
        .with_state(state);

    let host = &config.bind_addr;
    info!("starting server on {}!", host);
    let listener = tokio::net::TcpListener::bind(host).await.unwrap();
    axum::serve(listener, app).await.unwrap();

    Ok(())
}

struct AppError(anyhow::Error);

#[derive(Serialize)]
struct ErrorResponse {
    error: String,
}

impl IntoResponse for AppError {
    fn into_response(self) -> Response {
        error!("error handling request: {}", self.0);
        (
            StatusCode::INTERNAL_SERVER_ERROR,
            Json(ErrorResponse {
                error: self.0.to_string(),
            }),
        )
            .into_response()
    }
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

impl<E> From<E> for AppError
where
    E: Into<anyhow::Error>,
{
    fn from(err: E) -> Self {
        Self(err.into())
    }
}
