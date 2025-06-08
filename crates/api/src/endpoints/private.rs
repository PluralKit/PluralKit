use crate::ApiContext;
use axum::{extract::State, response::Json};
use fred::interfaces::*;
use libpk::state::ShardState;
use serde::Deserialize;
use serde_json::{json, Value};
use std::collections::HashMap;

#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
struct ClusterStats {
    pub guild_count: i32,
    pub channel_count: i32,
}

pub async fn discord_state(State(ctx): State<ApiContext>) -> Json<Value> {
    let mut shard_status = ctx
        .redis
        .hgetall::<HashMap<String, String>, &str>("pluralkit:shardstatus")
        .await
        .unwrap()
        .values()
        .map(|v| serde_json::from_str(v).expect("could not deserialize shard"))
        .collect::<Vec<ShardState>>();

    shard_status.sort_by(|a, b| b.shard_id.cmp(&a.shard_id));

    Json(json!({
        "shards": shard_status,
    }))
}

pub async fn meta(State(ctx): State<ApiContext>) -> Json<Value> {
    let stats = serde_json::from_str::<Value>(
        ctx.redis
            .get::<String, &'static str>("statsapi")
            .await
            .unwrap()
            .as_str(),
    )
    .unwrap();

    Json(stats)
}

use std::time::Duration;

use crate::util::json_err;
use axum::{
    extract,
    response::{IntoResponse, Response},
};
use hyper::StatusCode;
use libpk::config;
use pluralkit_models::{PKSystem, PKSystemConfig, PrivacyLevel};
use reqwest::ClientBuilder;

#[derive(serde::Deserialize, Debug)]
pub struct CallbackRequestData {
    redirect_domain: String,
    code: String,
    // state: String,
}

#[derive(serde::Serialize)]
struct CallbackDiscordData {
    client_id: String,
    client_secret: String,
    grant_type: String,
    redirect_uri: String,
    code: String,
}

pub async fn discord_callback(
    State(ctx): State<ApiContext>,
    extract::Json(request_data): extract::Json<CallbackRequestData>,
) -> Response {
    let client = ClientBuilder::new()
        .connect_timeout(Duration::from_secs(3))
        .timeout(Duration::from_secs(3))
        .build()
        .expect("error making client");

    let reqbody = serde_urlencoded::to_string(&CallbackDiscordData {
        client_id: config.discord.as_ref().unwrap().client_id.get().to_string(),
        client_secret: config.discord.as_ref().unwrap().client_secret.clone(),
        grant_type: "authorization_code".to_string(),
        redirect_uri: request_data.redirect_domain, // change this!
        code: request_data.code,
    })
    .expect("could not serialize");

    let discord_resp = client
        .post("https://discord.com/api/v10/oauth2/token")
        .header("content-type", "application/x-www-form-urlencoded")
        .body(reqbody)
        .send()
        .await
        .expect("failed to request discord");

    let Value::Object(discord_data) = discord_resp
        .json::<Value>()
        .await
        .expect("failed to deserialize discord response as json")
    else {
        panic!("discord response is not an object")
    };

    if !discord_data.contains_key("access_token") {
        return json_err(
            StatusCode::BAD_REQUEST,
            format!(
                "{{\"error\":\"{}\"\"}}",
                discord_data
                    .get("error_description")
                    .expect("missing error_description from discord")
                    .to_string()
            ),
        );
    };

    let token = format!(
        "Bearer {}",
        discord_data
            .get("access_token")
            .expect("missing access_token")
            .as_str()
            .unwrap()
    );

    let discord_client = twilight_http::Client::new(token);

    let user = discord_client
        .current_user()
        .await
        .expect("failed to get current user from discord")
        .model()
        .await
        .expect("failed to parse user model from discord");

    let system: Option<PKSystem> = sqlx::query_as(
        r#"
            select systems.*
                from accounts
                left join systems on accounts.system = systems.id
                where accounts.uid = $1
        "#,
    )
    .bind(user.id.get() as i64)
    .fetch_optional(&ctx.db)
    .await
    .expect("failed to query");

    let Some(system) = system else {
        return json_err(
            StatusCode::BAD_REQUEST,
            "user does not have a system registered".to_string(),
        );
    };

    let system_config: Option<PKSystemConfig> = sqlx::query_as(
        r#"
        select * from system_config where system = $1
        "#,
    )
    .bind(system.id)
    .fetch_optional(&ctx.db)
    .await
    .expect("failed to query");

    let system_config = system_config.unwrap();

    // create dashboard token for system

    let token = system.clone().token;

    (
        StatusCode::OK,
        serde_json::to_string(&serde_json::json!({
            "system": system.to_json(PrivacyLevel::Private),
            "config": system_config.to_json(),
            "user": user,
            "token": token,
        }))
        .expect("should not error"),
    )
        .into_response()
}
