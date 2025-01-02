use axum::http::StatusCode;
use std::fmt;

// todo
#[allow(dead_code)]
#[derive(Debug)]
pub struct PKError {
    pub response_code: StatusCode,
    pub json_code: i32,
    pub message: &'static str,
}

impl fmt::Display for PKError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{:?}", self)
    }
}

impl std::error::Error for PKError {}

#[allow(unused_macros)]
macro_rules! define_error {
    ( $name:ident, $response_code:expr, $json_code:expr, $message:expr ) => {
        const $name: PKError = PKError {
            response_code: $response_code,
            json_code: $json_code,
            message: $message,
        };
    };
}

// define_error! { GENERIC_BAD_REQUEST, StatusCode::BAD_REQUEST, 0, "400: Bad Request" }
