use axum::{Extension, Json, extract::State, response::IntoResponse};
use pk_macros::api_endpoint;
use serde_json::{Value, json};
use sqlx::Postgres;

use pluralkit_models::{PKSystem, PKSystemConfig, PrivacyLevel};

use crate::{ApiContext, auth::AuthState, error::fail};

#[api_endpoint]
pub async fn get_system_settings(
    Extension(auth): Extension<AuthState>,
    Extension(system): Extension<PKSystem>,
    State(ctx): State<ApiContext>,
) -> Json<Value> {
    let access_level = auth.access_level_for(&system);

    let mut config = match sqlx::query_as::<Postgres, PKSystemConfig>(
        "select * from system_config where system = $1",
    )
    .bind(system.id)
    .fetch_optional(&ctx.db)
    .await
    {
        Ok(Some(config)) => config,
        Ok(None) => fail!(
            system = system.id,
            "failed to find system config for existing system"
        ),
        Err(err) => fail!(?err, "failed to query system config"),
    };

    // fix this
    if config.name_format.is_none() {
        config.name_format = Some("{name} {tag}".to_string());
    }

    Ok(Json(match access_level {
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
    }))
}
