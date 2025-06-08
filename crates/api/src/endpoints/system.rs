use axum::{
    extract::State,
    http::StatusCode,
    response::{IntoResponse, Response},
    Extension,
};
use serde_json::json;
use sqlx::Postgres;
use tracing::error;

use pluralkit_models::{PKSystem, PKSystemConfig, PrivacyLevel};

use crate::{auth::AuthState, util::json_err, ApiContext};

pub async fn get_system_settings(
    Extension(auth): Extension<AuthState>,
    Extension(system): Extension<PKSystem>,
    State(ctx): State<ApiContext>,
) -> Response {
    let access_level = auth.access_level_for(&system);

    let config = match sqlx::query_as::<Postgres, PKSystemConfig>(
        "select * from system_config where system = $1",
    )
    .bind(system.id)
    .fetch_optional(&ctx.db)
    .await
    {
        Ok(Some(config)) => config,
        Ok(None) => {
            error!(
                system = system.id,
                "failed to find system config for existing system"
            );
            return json_err(
                StatusCode::INTERNAL_SERVER_ERROR,
                r#"{"message": "500: Internal Server Error", "code": 0}"#.to_string(),
            );
        }
        Err(err) => {
            error!(?err, "failed to query system config");
            return json_err(
                StatusCode::INTERNAL_SERVER_ERROR,
                r#"{"message": "500: Internal Server Error", "code": 0}"#.to_string(),
            );
        }
    };

    (
        StatusCode::OK,
        serde_json::to_string(&match access_level {
            PrivacyLevel::Private => config.to_json(),
            PrivacyLevel::Public => json!({
                "pings_enabled": config.pings_enabled,
                "latch_timeout": config.latch_timeout,
                "case_sensitive_proxy_tags": config.case_sensitive_proxy_tags,
                "proxy_error_message_enabled": config.proxy_error_message_enabled,
                "hid_display_split": config.hid_display_split,
                "hid_display_caps": config.hid_display_caps,
                "hid_list_padding": config.hid_list_padding,
                "proxy_switch": config.proxy_switch,
                "name_format": config.name_format,
            }),
        })
        .unwrap(),
    )
        .into_response()
}
