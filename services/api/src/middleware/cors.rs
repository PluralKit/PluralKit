use axum::{
    http::{HeaderMap, HeaderValue, Method, Request, StatusCode},
    middleware::Next,
    response::{IntoResponse, Response},
};

#[rustfmt::skip]
fn add_cors_headers(headers: &mut HeaderMap) {
    headers.append("Access-Control-Allow-Origin", HeaderValue::from_static("*"));
    headers.append("Access-Control-Allow-Methods", HeaderValue::from_static("*"));
    headers.append("Access-Control-Allow-Credentials", HeaderValue::from_static("true"));
    headers.append("Access-Control-Allow-Headers", HeaderValue::from_static("Content-Type, Authorization, sentry-trace, User-Agent"));
    headers.append("Access-Control-Expose-Headers", HeaderValue::from_static("X-RateLimit-Limit, X-RateLimit-Remaining, X-RateLimit-Reset"));
    headers.append("Access-Control-Max-Age", HeaderValue::from_static("86400"));
}

pub async fn cors<B>(request: Request<B>, next: Next<B>) -> Response {
    let mut response = if request.method() == Method::OPTIONS {
        StatusCode::OK.into_response()
    } else {
        next.run(request).await
    };

    add_cors_headers(response.headers_mut());

    response
}
