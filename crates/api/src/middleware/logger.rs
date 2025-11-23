use std::time::Instant;

use axum::{extract::MatchedPath, extract::Request, middleware::Next, response::Response};
use metrics::{counter, histogram};
use tracing::{Instrument, Level, info, span, warn};

use crate::{auth::AuthState, util::header_or_unknown};

// log any requests that take longer than 2 seconds
// todo: change as necessary
const MIN_LOG_TIME: u128 = 2_000;

pub async fn logger(request: Request, next: Next) -> Response {
    let method = request.method().clone();
    let headers = request.headers().clone();

    let remote_ip = header_or_unknown(headers.get("X-PluralKit-Client-IP"));
    let user_agent = header_or_unknown(headers.get("User-Agent"));

    let extensions = request.extensions().clone();

    let endpoint = extensions
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
    let response = next.run(request).instrument(request_span).await;
    let elapsed = start.elapsed().as_millis();

    let rext = response.extensions().clone();
    let auth = rext.get::<AuthState>();

    let system_id = if let Some(auth) = auth {
        auth.system_id()
            .map(|v| v.to_string())
            .unwrap_or("none".to_string())
    } else {
        "none".to_string()
    };

    let app_id = if let Some(auth) = auth {
        auth.app_id()
            .map(|v| v.to_string())
            .unwrap_or("none".to_string())
    } else {
        "none".to_string()
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
        status = response.status().as_str(),
        method = method.to_string(),
        endpoint,
        elapsed,
        user_agent,
        remote_ip,
        system_id,
        app_id,
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
