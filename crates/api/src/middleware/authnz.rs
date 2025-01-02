use axum::{
    extract::{Request, State},
    http::HeaderValue,
    middleware::Next,
    response::Response,
};
use tracing::error;

use crate::ApiContext;

use super::logger::DID_AUTHENTICATE_HEADER;

pub async fn authnz(State(ctx): State<ApiContext>, mut request: Request, next: Next) -> Response {
    let headers = request.headers_mut();
    headers.remove("x-pluralkit-systemid");
    let auth_header = headers
        .get("authorization")
        .map(|h| h.to_str().ok())
        .flatten();
    let mut authenticated = false;
    if let Some(auth_header) = auth_header {
        if let Some(system_id) =
            match libpk::db::repository::legacy_token_auth(&ctx.db, auth_header).await {
                Ok(val) => val,
                Err(err) => {
                    error!(?err, "failed to query authorization token in postgres");
                    None
                }
            }
        {
            headers.append(
                "x-pluralkit-systemid",
                HeaderValue::from_str(format!("{system_id}").as_str()).unwrap(),
            );
            authenticated = true;
        }
    }
    let mut response = next.run(request).await;
    if authenticated {
        response
            .headers_mut()
            .insert(DID_AUTHENTICATE_HEADER, HeaderValue::from_static("1"));
    }
    response
}
