use axum::{
    extract::{Request, State},
    http::StatusCode,
    middleware::Next,
    response::Response,
};
use tracing::error;

use crate::{util::json_err, ApiContext};

pub const INTERNAL_SYSTEMID_HEADER: &'static str = "x-pluralkit-systemid";
pub const INTERNAL_APPID_HEADER: &'static str = "x-pluralkit-appid";

// todo: auth should pass down models in request context
// not numerical ids in headers

pub async fn authnz(State(ctx): State<ApiContext>, mut request: Request, next: Next) -> Response {
    let headers = request.headers_mut();

    headers.remove(INTERNAL_SYSTEMID_HEADER);
    headers.remove(INTERNAL_APPID_HEADER);

    let mut authed_system_id: Option<i32> = None;
    let mut authed_app_id: Option<i32> = None;

    // fetch user authorization
    if let Some(system_auth_header) = headers
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
    if let Some(app_auth_header) = headers
        .get("x-pluralkit-app")
        .map(|h| h.to_str().ok())
        .flatten()
        && let Some(config_token2) = libpk::config
            .api
            .as_ref()
            .expect("missing api config")
            .temp_token2
            .as_ref()
        // this is NOT how you validate tokens
        // but this is low abuse risk so we're keeping it for now
        && app_auth_header == config_token2
    {
        authed_app_id = Some(1);
    }

    // add headers for ratelimiter / dotnet-api
    {
        let headers = request.headers_mut();
        if let Some(sid) = authed_system_id {
            headers.append(INTERNAL_SYSTEMID_HEADER, sid.into());
        }
        if let Some(aid) = authed_app_id {
            headers.append(INTERNAL_APPID_HEADER, aid.into());
        }
    }

    let mut response = next.run(request).await;

    // add headers for logger module (ugh)
    {
        let headers = response.headers_mut();
        if let Some(sid) = authed_system_id {
            headers.append(INTERNAL_SYSTEMID_HEADER, sid.into());
        }
        if let Some(aid) = authed_app_id {
            headers.append(INTERNAL_APPID_HEADER, aid.into());
        }
    }

    response
}
