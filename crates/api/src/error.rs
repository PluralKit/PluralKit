use axum::{
    http::StatusCode,
    response::{IntoResponse, Response},
};
use pluralkit_models::ValidationError;
use std::fmt;

// todo: model parse errors
#[derive(Debug)]
pub struct PKError {
    pub response_code: StatusCode,
    pub json_code: i32,
    pub message: &'static str,

    pub errors: Vec<ValidationError>,

    pub inner: Option<anyhow::Error>,
}

impl fmt::Display for PKError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{:?}", self)
    }
}

impl Clone for PKError {
    fn clone(&self) -> PKError {
        if self.inner.is_some() {
            panic!("cannot clone PKError with inner error");
        }
        PKError {
            response_code: self.response_code,
            json_code: self.json_code,
            message: self.message,
            inner: None,
            errors: self.errors.clone(),
        }
    }
}

// can't `impl From<Vec<ValidationError>>`
// because "upstream crate may add a new impl" >:(
impl PKError {
    pub fn from_validation_errors(errs: Vec<ValidationError>) -> Self {
        Self {
            message: "Error parsing JSON model",
            json_code: 40001,
            errors: errs,
            response_code: StatusCode::BAD_REQUEST,
            inner: None,
        }
    }
}

impl<E> From<E> for PKError
where
    E: std::fmt::Display + Into<anyhow::Error>,
{
    fn from(err: E) -> Self {
        let mut res = GENERIC_SERVER_ERROR.clone();
        res.inner = Some(err.into());
        res
    }
}

impl IntoResponse for PKError {
    fn into_response(self) -> Response {
        if let Some(inner) = self.inner {
            tracing::error!(?inner, "error returned from handler");
        }
        let json = if self.errors.len() > 0 {
            serde_json::json!({
                "message": self.message,
                "code": self.json_code,
                "errors": self.errors,
            })
        } else {
            serde_json::json!({
                "message": self.message,
                "code": self.json_code,
            })
        };
        crate::util::json_err(self.response_code, serde_json::to_string(&json).unwrap())
    }
}

#[macro_export]
macro_rules! fail {
    ($($stuff:tt)+) => {{
        tracing::error!($($stuff)+);
        return Err($crate::error::GENERIC_SERVER_ERROR);
    }};
}

#[macro_export]
macro_rules! fail_html {
    ($($stuff:tt)+) => {{
        tracing::error!($($stuff)+);
        return (axum::http::StatusCode::INTERNAL_SERVER_ERROR, "internal server error").into_response();
    }};
}

macro_rules! define_error {
    ( $name:ident, $response_code:expr, $json_code:expr, $message:expr ) => {
        #[allow(dead_code)]
        pub const $name: PKError = PKError {
            response_code: $response_code,
            json_code: $json_code,
            message: $message,
            inner: None,
            errors: vec![],
        };
    };
}

define_error! { GENERIC_AUTH_ERROR, StatusCode::UNAUTHORIZED, 0, "401: Missing or invalid Authorization header" }
define_error! { GENERIC_BAD_REQUEST, StatusCode::BAD_REQUEST, 0, "400: Bad Request" }
define_error! { GENERIC_SERVER_ERROR, StatusCode::INTERNAL_SERVER_ERROR, 0, "500: Internal Server Error" }

define_error! { NOT_OWN_MEMBER, StatusCode::FORBIDDEN, 30006, "Target member is not part of your system." }
define_error! { NOT_OWN_GROUP, StatusCode::FORBIDDEN, 30007, "Target group is not part of your system." }

define_error! { TARGET_MEMBER_NOT_FOUND, StatusCode::BAD_REQUEST, 40010, "Target member not found." }
define_error! { TARGET_GROUP_NOT_FOUND, StatusCode::BAD_REQUEST, 40011, "Target group not found." }
