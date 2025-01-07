use crate::{ApiContext, util::json_err};
use pluralkit_models::{SystemId, PKApiKey, ApiKeyType, PKSystem};

use axum::{
    extract::State,
    response::{Response, IntoResponse, Json},
    http::StatusCode,
};

#[derive(serde::Deserialize)]
pub struct NewApiKeyRequestData {
    #[serde(default)]
    check: bool,
    system: SystemId,
    name: Option<String>,
    scopes: Vec<String>,
}

pub async fn create_api_key(
    State(ctx): State<ApiContext>,
    Json(req): Json<NewApiKeyRequestData>,
) -> Response {
    let system: Option<PKSystem> = sqlx::query_as("select * from systems where id = $1")
        .bind(req.system)
        .fetch_optional(&ctx.db)
        .await
        .expect("failed to query system");

    if system.is_none() {
        return json_err(
            StatusCode::BAD_REQUEST,
            r#"{"message": "no system found!?", "internal": true}"#.to_string(),
        );
    }

    let system = system.unwrap();

    // sanity check requested scopes
    if req.scopes.len() < 1 {
        return json_err(
            StatusCode::BAD_REQUEST,
            r#"{"message": "no scopes provided", "internal": true}"#.to_string(),
        );
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
                "switches" => true,
                _ => false,
            },
            _ => false,
        };

        if !ok {
            return json_err(
                StatusCode::BAD_REQUEST,
                format!(
                    r#"{{"internal":true,"error":"invalid scope: {}"}}"#,
                    scope,
                ),
            );
        }
    }

    if req.check {
        return (
            StatusCode::OK,
            serde_json::to_string(&serde_json::json!({
                "valid": true,
            }))
            .expect("should not error"),
        )
            .into_response();
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

    (
        StatusCode::OK,
        serde_json::to_string(&serde_json::json!({
            "token": token,
        }))
        .expect("should not error"),
    )
        .into_response()
}
