use crate::error::PKError;
use axum::{
    http::{HeaderValue, StatusCode},
    response::IntoResponse,
};
use serde_json::{json, to_string, Value};
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

#[allow(dead_code)]
pub fn wrapper<F>(handler: F) -> impl Fn() -> axum::response::Response
where
    F: Fn() -> anyhow::Result<Value>,
{
    move || match handler() {
        Ok(v) => (StatusCode::OK, to_string(&v).unwrap()).into_response(),
        Err(error) => match error.downcast_ref::<PKError>() {
            Some(pkerror) => json_err(
                pkerror.response_code,
                to_string(&json!({ "message": pkerror.message, "code": pkerror.json_code }))
                    .unwrap(),
            ),
            None => {
                error!(
                    "error in handler {}: {:#?}",
                    std::any::type_name::<F>(),
                    error
                );
                json_err(
                    StatusCode::INTERNAL_SERVER_ERROR,
                    r#"{"message": "500: Internal Server Error", "code": 0}"#.to_string(),
                )
            }
        },
    }
}

pub fn handle_panic(err: Box<dyn std::any::Any + Send + 'static>) -> axum::response::Response {
    error!("caught panic from handler: {:#?}", err);
    json_err(
        StatusCode::INTERNAL_SERVER_ERROR,
        r#"{"message": "500: Internal Server Error", "code": 0}"#.to_string(),
    )
}

// todo: make 500 not duplicated
pub fn json_err(code: StatusCode, text: String) -> axum::response::Response {
    let mut response = (code, text).into_response();
    let headers = response.headers_mut();
    headers.insert("content-type", HeaderValue::from_static("application/json"));
    response
}
