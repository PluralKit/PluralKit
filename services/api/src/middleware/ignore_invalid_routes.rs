use axum::{
    extract::MatchedPath,
    extract::Request,
    http::StatusCode,
    middleware::Next,
    response::{IntoResponse, Response},
};

use crate::util::header_or_unknown;

fn is_trying_to_use_v1_path_on_v2(path: &str) -> bool {
    path.starts_with("/v2/s/")
        || path.starts_with("/v2/m/")
        || path.starts_with("/v2/a/")
        || path.starts_with("/v2/msg/")
        || path == "/v2/s"
        || path == "/v2/m"
}

pub async fn ignore_invalid_routes(request: Request, next: Next) -> Response {
    let path = request
        .extensions()
        .get::<MatchedPath>()
        .cloned()
        .map(|v| v.as_str().to_string())
        .unwrap_or("unknown".to_string());
    let user_agent = header_or_unknown(request.headers().get("User-Agent"));

    if request.uri().path().starts_with("/v1") {
        (
            StatusCode::GONE,
            r#"{"message":"Unsupported API version","code":0}"#,
        )
            .into_response()
    } else if is_trying_to_use_v1_path_on_v2(request.uri().path()) {
        (
            StatusCode::BAD_REQUEST,
            r#"{"message":"Invalid path for API version","code":0}"#,
        )
            .into_response()
    }
    // we ignored v1 routes earlier, now let's ignore all non-v2 routes
    else if !request.uri().clone().path().starts_with("/v2")
        && !request.uri().clone().path().starts_with("/private")
    {
        return (
            StatusCode::BAD_REQUEST,
            r#"{"message":"Unsupported API version","code":0}"#,
        )
            .into_response();
    } else if path == "unknown" {
        // current prod api responds with 404 with empty body to invalid endpoints
        // just doing that here as well but i'm not sure if it's the correct behaviour
        return StatusCode::NOT_FOUND.into_response();
    }
    // yes, technically because of how we parse headers this will break for user-agents literally set to "unknown"
    // but "unknown" isn't really a valid user-agent
    else if user_agent == "unknown" {
        // please set a valid user-agent
        return StatusCode::BAD_REQUEST.into_response();
    } else {
        next.run(request).await
    }
}
