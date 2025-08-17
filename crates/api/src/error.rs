use axum::{
    http::StatusCode,
    response::{IntoResponse, Response},
};
use std::fmt;

// todo: model parse errors
#[derive(Debug)]
pub struct PKError {
    pub response_code: StatusCode,
    pub json_code: i32,
    pub message: &'static str,

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
        crate::util::json_err(
            self.response_code,
            serde_json::to_string(&serde_json::json!({
                "message": self.message,
                "code": self.json_code,
            }))
            .unwrap(),
        )
    }
}

macro_rules! fail {
    ($($stuff:tt)+) => {{
        tracing::error!($($stuff)+);
        return Err(crate::error::GENERIC_SERVER_ERROR);
    }};
}

pub(crate) use fail;

macro_rules! define_error {
    ( $name:ident, $response_code:expr, $json_code:expr, $message:expr ) => {
        #[allow(dead_code)]
        pub const $name: PKError = PKError {
            response_code: $response_code,
            json_code: $json_code,
            message: $message,
            inner: None,
        };
    };
}

define_error! { GENERIC_BAD_REQUEST, StatusCode::BAD_REQUEST, 0, "400: Bad Request" }
// define_error! { GENERIC_UNAUTHORIZED, StatusCode::UNAUTHORIZED, 0, "401: Missing or invalid Authorization header" }
define_error! { FORBIDDEN_INTERNAL_ROUTE, StatusCode::FORBIDDEN, 0, "403: Forbidden to access this endpoint" }
define_error! { GENERIC_SERVER_ERROR, StatusCode::INTERNAL_SERVER_ERROR, 0, "500: Internal Server Error" }
