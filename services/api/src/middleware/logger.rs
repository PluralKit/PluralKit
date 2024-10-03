use std::time::Instant;

use axum::{extract::MatchedPath, extract::Request, middleware::Next, response::Response};
use metrics::histogram;
use tracing::{info, span, warn, Instrument, Level};

use crate::util::header_or_unknown;

// log any requests that take longer than 2 seconds
// todo: change as necessary
const MIN_LOG_TIME: u128 = 2_000;

pub async fn logger(request: Request, next: Next) -> Response {
    let method = request.method().clone();

    let request_id = header_or_unknown(request.headers().get("Fly-Request-Id"));
    let remote_ip = header_or_unknown(request.headers().get("Fly-Client-IP"));
    let user_agent = header_or_unknown(request.headers().get("User-Agent"));

    let endpoint = request
        .extensions()
        .get::<MatchedPath>()
        .cloned()
        .map(|v| v.as_str().to_string())
        .unwrap_or("unknown".to_string());

    let uri = request.uri().clone();

    let request_id_span = span!(
        Level::INFO,
        "request",
        request_id,
        remote_ip,
        method = method.as_str(),
        endpoint = endpoint.clone(),
        user_agent
    );

    let start = Instant::now();
    let response = next.run(request).instrument(request_id_span).await;
    let elapsed = start.elapsed().as_millis();

    info!(
        "{} handled request for {} {} in {}ms",
        response.status(),
        method,
        endpoint,
        elapsed
    );
    histogram!(
        "pk_http_requests",
        "method" => method.to_string(),
        "route" => endpoint.clone(),
        "status" => response.status().to_string()
    )
    .record((elapsed as f64) / 1_000_f64);

    if elapsed > MIN_LOG_TIME {
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
