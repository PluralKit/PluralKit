use crate::{util::json_err, AuthState, ApiContext};
use pluralkit_models::{ApiKeyType, PKApiKey, PKSystem, SystemId};
use pk_macros::api_internal_endpoint;

use axum::{
    extract::State,
    http::StatusCode,
    response::{IntoResponse, Json, Response},
	Extension,
};

use sqlx::Postgres;

#[derive(serde::Deserialize)]
pub struct NewApiKeyRequestData {
    #[serde(default)]
    check: bool,
    system: SystemId,
    name: Option<String>,
    scopes: Vec<String>,
}

#[api_internal_endpoint]
pub async fn create_api_key_user(
    State(ctx): State<ApiContext>,
	Extension(auth): Extension<AuthState>,
    Json(req): Json<NewApiKeyRequestData>,
) -> Response {
    let system: Option<PKSystem> = sqlx::query_as("select * from systems where id = $1")
        .bind(req.system)
        .fetch_optional(&ctx.db)
        .await
        .expect("failed to query system");

    if system.is_none() {
        return Ok(json_err(
            StatusCode::BAD_REQUEST,
            r#"{"message": "no system found!?", "internal": true}"#.to_string(),
        ));
    }

    let system = system.unwrap();

    // sanity check requested scopes
    if req.scopes.len() < 1 {
        return Ok(json_err(
            StatusCode::BAD_REQUEST,
            r#"{"message": "no scopes provided", "internal": true}"#.to_string(),
        ));
    }
    for scope in req.scopes.iter() {
        let parts = scope.split(":").collect::<Vec<&str>>();
        let ok = match &parts[..] {
            ["identify"] => true,
            ["publicread", n] | ["read", n] | ["write", n] => match *n {
                "all" => true,
                "system" => true,
                "members" => true,
                "groups" => true,
                "fronters" => true,
                "switches" => true,
                _ => false,
            },
            _ => false,
        };

        if !ok {
            return Err(crate::error::GENERIC_BAD_REQUEST);
        }
    }

    if req.check {
        return Ok((
            StatusCode::OK,
            serde_json::to_string(&serde_json::json!({
                "valid": true,
            }))
            .expect("should not error"),
        ).into_response());
    }

    let token: PKApiKey = sqlx::query_as(
        r#"
            insert into api_keys
            (
                system,
                kind,
                scopes,
                name
            )
            values
                ($1, $2::api_key_type, $3::text[], $4)
            returning *
        "#,
    )
    .bind(system.id)
    .bind(ApiKeyType::UserCreated)
    .bind(req.scopes)
    .bind(req.name)
    .fetch_one(&ctx.db)
    .await
    .expect("failed to create token");

    let token = token.to_header_str(system.clone().uuid, &ctx.token_privatekey);

    Ok((
        StatusCode::OK,
        serde_json::to_string(&serde_json::json!({
            "valid": true,
            "token": token,
        }))
        .expect("should not error"),
    ).into_response())
}
