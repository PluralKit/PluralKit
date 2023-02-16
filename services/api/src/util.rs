use axum::{
    body::Body,
    http::{HeaderValue, Request, Response, StatusCode, Uri},
    response::IntoResponse,
};
use tracing::error;

pub fn header_or_unknown(header: Option<&HeaderValue>) -> &str {
    if let Some(value) = header {
        match value.to_str() {
            Ok(v) => v,
            Err(err) => {
                error!("failed to parse header value {:#?}: {:#?}", value, err);
                "failed to parse"
            }
        }
    } else {
        "unknown"
    }
}

pub async fn rproxy(req: Request<Body>) -> Response<Body> {
    let uri = Uri::from_static(&libpk::config.api.remote_url).to_string();

    match hyper_reverse_proxy::call("0.0.0.0".parse().unwrap(), &uri[..uri.len() - 1], req).await {
        Ok(response) => response,
        Err(error) => {
            error!("error proxying request: {:?}", error);
            Response::builder()
                .status(StatusCode::INTERNAL_SERVER_ERROR)
                .body(Body::empty())
                .unwrap()
        }
    }
}

pub fn json_err(code: StatusCode, text: String) -> axum::response::Response {
    let mut response = (code, text).into_response();
    let headers = response.headers_mut();
    headers.insert("content-type", HeaderValue::from_static("application/json"));
    response
}
