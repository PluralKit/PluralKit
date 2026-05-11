#![feature(if_let_guard)]
mod auth;
pub mod error;
pub mod middleware;
pub mod util;

#[derive(Clone)]
pub struct AvatarServiceClient {
    client: reqwest::Client,
    base_url: String,
}

impl AvatarServiceClient {
    pub fn new(base_url: String) -> Self {
        let client = reqwest::Client::builder()
            .timeout(std::time::Duration::from_secs(30))
            .build()
            .expect("failed to build avatar service HTTP client");
        Self { client, base_url }
    }

    pub async fn pull(
        &self,
        url: String,
        kind: libpk::db::types::avatars::ImageKind,
    ) -> Result<reqwest::Response, reqwest::Error> {
        let mut req =
            self.client
                .post(format!("{}/pull", self.base_url))
                .json(&serde_json::json!({
                    "url": url,
                    "kind": kind,
                }));

        if let Some(ref token) = libpk::config.internal_auth {
            req = req.header("x-pluralkit-internalauth", token);
        }

        req.send().await
    }
}

#[derive(Clone)]
pub struct ApiContext {
    pub db: sqlx::postgres::PgPool,
    pub redis: fred::clients::RedisPool,
    pub s3_client: aws_sdk_s3::Client,
    pub storage_bucket: String,
    pub uploads_bucket: String,
    pub avatar_service: AvatarServiceClient,
}
