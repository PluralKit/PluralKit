use axum::{
    extract::Request,
    http::{HeaderMap, HeaderValue, Method, StatusCode},
    middleware::Next,
    response::{IntoResponse, Response},
};

#[rustfmt::skip]
fn add_cors_headers(headers: &mut HeaderMap) {
    headers.append("Access-Control-Allow-Origin", HeaderValue::from_static("*"));
    headers.append("Access-Control-Allow-Methods", HeaderValue::from_static("*"));
    headers.append("Access-Control-Allow-Credentials", HeaderValue::from_static("true"));
    headers.append("Access-Control-Allow-Headers", HeaderValue::from_static("Content-Type, Authorization, sentry-trace, User-Agent"));
    headers.append("Access-Control-Expose-Headers", HeaderValue::from_static("X-PluralKit-Version, X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset, X-RateLimit-Scope"));
    headers.append("Access-Control-Max-Age", HeaderValue::from_static("86400"));
}

pub async fn cors(request: Request, next: Next) -> Response {
    let mut response = if request.method() == Method::OPTIONS {
        StatusCode::OK.into_response()
    } else {
        next.run(request).await
    };

    add_cors_headers(response.headers_mut());

    response
}
