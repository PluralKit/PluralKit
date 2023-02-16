use std::time::Instant;

use axum::{extract::MatchedPath, http::Request, middleware::Next, response::Response};
use tracing::{info, span, Instrument, Level};

use crate::util::header_or_unknown;

pub async fn logger<B>(request: Request<B>, next: Next<B>) -> Response {
    let method = request.method().clone();

    let request_id = header_or_unknown(request.headers().get("Fly-Request-Id"));
    let remote_ip = header_or_unknown(request.headers().get("Fly-Client-IP"));
    let user_agent = header_or_unknown(request.headers().get("User-Agent"));

    let path = request
        .extensions()
        .get::<MatchedPath>()
        .cloned()
        .map(|v| v.as_str().to_string())
        .unwrap_or("unknown".to_string());

    // todo: prometheus metrics

    let request_id_span = span!(
        Level::INFO,
        "request",
        request_id,
        remote_ip,
        method = method.as_str(),
        path,
        user_agent
    );

    let start = Instant::now();
    let response = next.run(request).instrument(request_id_span).await;
    let elapsed = start.elapsed().as_millis();

    info!("handled request for {} {} in {}ms", method, path, elapsed);

    response
}
