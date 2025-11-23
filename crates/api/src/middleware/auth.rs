use axum::{
    extract::{Request, State},
    http::StatusCode,
    middleware::Next,
    response::Response,
};

use subtle::ConstantTimeEq;

use tracing::error;

use crate::auth::AuthState;
use crate::{ApiContext, util::json_err};

pub async fn auth(State(ctx): State<ApiContext>, mut req: Request, next: Next) -> Response {
    let mut authed_system_id: Option<i32> = None;
    let mut authed_app_id: Option<i32> = None;

    // fetch user authorization
    if let Some(system_auth_header) = req
        .headers()
        .get("authorization")
        .map(|h| h.to_str().ok())
        .flatten()
        && let Some(system_id) =
            match libpk::db::repository::legacy_token_auth(&ctx.db, system_auth_header).await {
                Ok(val) => val,
                Err(err) => {
                    error!(?err, "failed to query authorization token in postgres");
                    return json_err(
                        StatusCode::INTERNAL_SERVER_ERROR,
                        r#"{"message": "500: Internal Server Error", "code": 0}"#.to_string(),
                    );
                }
            }
    {
        authed_system_id = Some(system_id);
    }

    // fetch app authorization
    // todo: actually fetch it from db
    if let Some(app_auth_header) = req
        .headers()
        .get("x-pluralkit-app")
        .map(|h| h.to_str().ok())
        .flatten()
        && let Some(config_token2) = libpk::config
            .api
            .as_ref()
            .expect("missing api config")
            .temp_token2
            .as_ref()
        && app_auth_header
            .as_bytes()
            .ct_eq(config_token2.as_bytes())
            .into()
    {
        authed_app_id = Some(1);
    }

    // todo: fix syntax
    let internal = if req.headers().get("x-pluralkit-client-ip").is_none()
        && let Some(auth_header) = req
            .headers()
            .get("x-pluralkit-internalauth")
            .map(|h| h.to_str().ok())
            .flatten()
        && let Some(real_token) = libpk::config.internal_auth.clone()
        && auth_header.as_bytes().ct_eq(real_token.as_bytes()).into()
    {
        true
    } else {
        false
    };

    req.extensions_mut()
        .insert(AuthState::new(authed_system_id, authed_app_id, internal));

    let mut res = next.run(req).await;

    res.extensions_mut()
        .insert(AuthState::new(authed_system_id, authed_app_id, internal));

    res
}
