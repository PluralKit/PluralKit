use std::time::Instant;

use axum::{extract::MatchedPath, extract::Request, middleware::Next, response::Response};
use tracing::{info, span, warn, Instrument, Level};

// log any requests that take longer than 2 seconds
// todo: change as necessary
const MIN_LOG_TIME: u128 = 2_000;

pub async fn logger(request: Request, next: Next) -> Response {
    let method = request.method().clone();

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
        method = method.as_str(),
        endpoint = endpoint.clone(),
    );

    let start = Instant::now();
    let response = next.run(request).instrument(request_id_span).await;
    let elapsed = start.elapsed().as_millis();

    info!(
        "{} handled request for {} {} in {}ms",
        response.status(),
        method,
        uri.path(),
        elapsed
    );

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
