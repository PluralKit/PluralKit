use axum::{
    Extension, Json,
    extract::State,
    http::StatusCode,
    response::{IntoResponse, Response},
};
use serde::{Deserialize, Serialize};

use crate::auth::AuthState;
use api::ApiContext;

#[derive(Deserialize)]
pub(crate) struct ValidateTokenRequest {
    csrf_token: String,
    token: String,
}

#[derive(Serialize)]
struct ValidateTokenResponse {
    system_id: i32,
}

#[derive(Serialize)]
struct ValidateTokenError {
    error: String,
}

pub(crate) async fn validate_token(
    State(ctx): State<ApiContext>,
    Extension(session): Extension<AuthState>,
    Json(body): Json<ValidateTokenRequest>,
) -> Response {
    if body.csrf_token != session.csrf_token {
        return (
            StatusCode::FORBIDDEN,
            Json(ValidateTokenError {
                error: "Invalid CSRF token.".to_string(),
            }),
        )
            .into_response();
    }

    let system_id = match libpk::db::repository::legacy_token_auth(&ctx.db, &body.token).await {
        Ok(Some(id)) => id,
        Ok(None) => {
            return (
                StatusCode::BAD_REQUEST,
                Json(ValidateTokenError {
                    error: "Invalid system token.".to_string(),
                }),
            )
                .into_response();
        }
        Err(err) => {
            tracing::error!(?err, "failed to validate system token");
            return (
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(ValidateTokenError {
                    error: "Failed to validate token.".to_string(),
                }),
            )
                .into_response();
        }
    };

    Json(ValidateTokenResponse { system_id }).into_response()
}
