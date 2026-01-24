use crate::ApiContext;
use crate::auth::AuthState;
use crate::error::{GENERIC_BAD_REQUEST, GENERIC_NOT_FOUND, fail};
use axum::Extension;
use axum::extract::{Path, Request};
use axum::http::HeaderValue;
use axum::response::IntoResponse;
use axum::{extract::State, response::Json};
use hyper::Uri;
use libpk::config;
use libpk::db::repository::avatars as avatars_db;
use libpk::db::types::avatars::*;
use pk_macros::api_endpoint;
use pluralkit_models::PKSystemConfig;
use serde::Serialize;
use sqlx::Postgres;
use sqlx::types::Uuid;
use sqlx::types::chrono::Utc;
use std::result::Result::Ok;
use tracing::warn;

#[derive(Serialize)]
struct APIImage {
    url: String,
    proxy_url: Option<String>,
}

#[api_endpoint]
pub async fn image_data(
    State(ctx): State<ApiContext>,
    Path((system_uuid, image_uuid)): Path<(Uuid, Uuid)>,
) -> Json<APIImage> {
    let img: Image = match avatars_db::get_by_id(&ctx.db, system_uuid, image_uuid).await {
        Ok(Some(img)) => img,
        Ok(None) => return Err(GENERIC_NOT_FOUND),
        Err(err) => fail!(?err, "failed to query image"),
    };
    let mut proxy_url: Option<String> = None;
    if let Some(proxy_hash) = img.meta.proxy_image {
        let proxy_img = match avatars_db::get_by_hash(&ctx.db, proxy_hash.to_string()).await {
            Ok(Some(img)) => img,
            Ok(None) => {
                warn!(
                    system_uuid = system_uuid.to_string(),
                    image_uuid = image_uuid.to_string(),
                    "failed to find proxy image"
                );
                return Err(GENERIC_NOT_FOUND);
            }
            Err(err) => fail!(?err, "failed to query proxy image"),
        };
        proxy_url = Some(proxy_img.url)
    }
    return Ok(Json(APIImage {
        url: img.data.url,
        proxy_url: proxy_url,
    }));
}

#[api_endpoint]
pub async fn upload(
    Extension(auth): Extension<AuthState>,
    State(ctx): State<ApiContext>,
    mut req: Request,
) -> impl IntoResponse {
    let Some(system_id) = auth.system_id() else {
        return Err(crate::error::GENERIC_AUTH_ERROR);
    };

    let uuid: Uuid = match sqlx::query_scalar("select uuid from systems where id = $1")
        .bind(system_id)
        .fetch_optional(&ctx.db)
        .await
    {
        Ok(Some(uuid)) => uuid,
        Ok(None) => fail!(
            system = system_id,
            "failed to find uuid for existing system"
        ),
        Err(err) => fail!(?err, "failed to query system uuid"),
    };

    let sys_config = match sqlx::query_as::<Postgres, PKSystemConfig>(
        "select * from system_config where system = $1",
    )
    .bind(system_id)
    .fetch_optional(&ctx.db)
    .await
    {
        Ok(Some(sys_config)) => sys_config,
        Ok(None) => fail!(
            system = system_id,
            "failed to find system config for existing system"
        ),
        Err(err) => fail!(?err, "failed to query system config"),
    };
    if !sys_config.premium_lifetime {
        if let Some(premium_until) = sys_config.premium_until {
            if premium_until < Utc::now().naive_utc() {
                return Err(GENERIC_BAD_REQUEST);
            }
        } else {
            return Err(GENERIC_BAD_REQUEST);
        }
    }

    let url = format!(
        "{}/upload",
        config
            .api
            .as_ref()
            .unwrap()
            .avatars_service_url
            .clone()
            .expect("expected avatars url")
    );

    *req.uri_mut() = Uri::try_from(url).unwrap();
    let headers = req.headers_mut();
    headers.append(
        "x-pluralkit-systemuuid",
        HeaderValue::from_str(&uuid.to_string()).expect("expected valid uuid for header"),
    );

    Ok(ctx.rproxy_client.request(req).await?.into_response())
}
