use std::time::Instant;

use axum::{extract::MatchedPath, extract::Request, middleware::Next, response::Response};
use metrics::{counter, histogram};
use tracing::{info, span, warn, Instrument, Level};

use crate::{
    middleware::authnz::{INTERNAL_APPID_HEADER, INTERNAL_SYSTEMID_HEADER},
    util::header_or_unknown,
};

// log any requests that take longer than 2 seconds
// todo: change as necessary
const MIN_LOG_TIME: u128 = 2_000;

pub async fn logger(request: Request, next: Next) -> Response {
    let method = request.method().clone();

    let remote_ip = header_or_unknown(request.headers().get("X-PluralKit-Client-IP"));
    let user_agent = header_or_unknown(request.headers().get("User-Agent"));

    let endpoint = request
        .extensions()
        .get::<MatchedPath>()
        .cloned()
        .map(|v| v.as_str().to_string())
        .unwrap_or("unknown".to_string());

    let uri = request.uri().clone();

    let request_span = span!(
        Level::INFO,
        "request",
        remote_ip,
        method = method.as_str(),
        endpoint = endpoint.clone(),
        user_agent
    );

    let start = Instant::now();
    let mut response = next.run(request).instrument(request_span).await;
    let elapsed = start.elapsed().as_millis();

    let (system_id, app_id) = {
        let headers = response.headers_mut();
        (
            headers
                .remove(INTERNAL_SYSTEMID_HEADER)
                .map(|h| h.to_str().ok().map(|v| v.to_string()))
                .flatten()
                .unwrap_or("none".to_string()),
            headers
                .remove(INTERNAL_APPID_HEADER)
                .map(|h| h.to_str().ok().map(|v| v.to_string()))
                .flatten()
                .unwrap_or("none".to_string()),
        )
    };

    counter!(
        "pluralkit_api_requests",
        "method" => method.to_string(),
        "endpoint" => endpoint.clone(),
        "status" => response.status().to_string(),
        "system_id" => system_id.to_string(),
        "app_id" => app_id.to_string(),
    )
    .increment(1);
    histogram!(
        "pluralkit_api_requests_bucket",
        "method" => method.to_string(),
        "endpoint" => endpoint.clone(),
        "status" => response.status().to_string(),
        "system_id" => system_id.to_string(),
        "app_id" => app_id.to_string(),
    )
    .record(elapsed as f64 / 1_000_f64);

    info!(
        "{} handled request for {} {} in {}ms",
        response.status(),
        method,
        endpoint,
        elapsed
    );

    if elapsed > MIN_LOG_TIME {
        counter!(
            "pluralkit_api_slow_requests_count",
            "method" => method.to_string(),
            "endpoint" => endpoint.clone(),
            "status" => response.status().to_string(),
            "system_id" => system_id.to_string(),
            "app_id" => app_id.to_string(),
        )
        .increment(1);

        warn!(
            "request to {} full path {} (endpoint {}) took a long time ({}ms)!",
            method,
            uri.path(),
            endpoint,
            elapsed
        )
    }

    response
}
