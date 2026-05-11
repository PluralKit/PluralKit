use axum::{
    extract::{Request, State},
    http::StatusCode,
    middleware::Next,
    response::Response,
};
use std::str::FromStr;

use subtle::ConstantTimeEq;

use tracing::{debug, error};

use crate::{ApiContext, util::json_err};
use crate::{
    auth::{AuthState, INTERNAL_SYSTEMID_HEADER},
    reject_request,
};
use libpk::db::repository::premium as premium_db;
use libpk::db::repository::premium::PremiumAllowances;

pub async fn auth(State(ctx): State<ApiContext>, mut req: Request, next: Next) -> Response {
    let internal = req.headers().get("x-pluralkit-client-ip").is_none()
        && req
            .headers()
            .get("x-pluralkit-internalauth")
            .and_then(|h| h.to_str().ok())
            .zip(libpk::config.internal_auth.clone())
            .is_some_and(|(auth_header, real_token)| {
                auth_header.as_bytes().ct_eq(real_token.as_bytes()).into()
            });

    if let Some(ua) = req.headers().get("user-agent")
        && ua == "pluralkit-dotnet-bot"
        && !internal
    {
        reject_request!();
    }

    // fetch user authorization
    let authed_system_id = if internal
        && let Some(sid_from_internal) = req
            .headers()
            .get(INTERNAL_SYSTEMID_HEADER)
            .and_then(|h| h.to_str().ok())
            .and_then(|v| i32::from_str(v).ok())
    {
        Some(sid_from_internal)
    } else if let Some(system_auth_header) = req
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
        Some(system_id)
    } else {
        None
    };

    // fetch app authorization
    // todo: actually fetch it from db
    let authed_app_id = if let Some(app_auth_header) = req
        .headers()
        .get("x-pluralkit-app")
        .map(|h| h.to_str().ok())
        .flatten()
        && let Some(config_token2) = libpk::config.api().temp_token2.as_ref()
        && app_auth_header
            .as_bytes()
            .ct_eq(config_token2.as_bytes())
            .into()
    {
        Some(1)
    } else {
        None
    };

    // fetch premium for authenticated system
    // i'm not sure if fetching this here is correct (we only fetch the system id and not full object here)
    // but since it's used for authnz it's probably fine?
    let premium: Option<PremiumAllowances> = if let Some(system_id) = authed_system_id {
        match premium_db::get_system_premium(&ctx.db, system_id).await {
            Ok(val) => val,
            Err(err) => {
                error!(?err, "failed to query premium status");
                None
            }
        }
    } else {
        None
    };

    let auth_state: AuthState = AuthState::new(authed_system_id, authed_app_id, internal, premium);

    req.extensions_mut().insert(auth_state.clone());

    let mut res = next.run(req).await;

    res.extensions_mut().insert(auth_state);

    res
}
