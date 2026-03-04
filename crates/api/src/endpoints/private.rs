use crate::{ApiContext, auth::AuthState, fail};
use axum::{
    Extension,
    extract::{Path, State},
    response::Json,
};
use fred::interfaces::*;
use libpk::state::ShardState;
use pk_macros::api_endpoint;
use serde::Deserialize;
use serde_json::{Value, json};
use sqlx::Postgres;
use std::collections::HashMap;

#[allow(dead_code)]
#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
struct ClusterStats {
    pub guild_count: i32,
    pub channel_count: i32,
}

#[api_endpoint]
pub async fn discord_state(State(ctx): State<ApiContext>) -> Json<Value> {
    let mut shard_status = ctx
        .redis
        .hgetall::<HashMap<String, String>, &str>("pluralkit:shardstatus")
        .await?
        .values()
        .map(|v| serde_json::from_str(v).expect("could not deserialize shard"))
        .collect::<Vec<ShardState>>();

    shard_status.sort_by(|a, b| b.shard_id.cmp(&a.shard_id));

    Ok(Json(json!({
        "shards": shard_status,
    })))
}

#[api_endpoint]
pub async fn meta(State(ctx): State<ApiContext>) -> Json<Value> {
    let stats = serde_json::from_str::<Value>(
        ctx.redis
            .get::<String, &'static str>("statsapi")
            .await?
            .as_str(),
    )?;

    Ok(Json(stats))
}

use std::time::Duration;

use crate::util::json_err;
use axum::{
    extract,
    response::{IntoResponse, Response},
};
use hyper::StatusCode;
use libpk::config;
use pluralkit_models::{PKDashView, PKSystem, PKSystemConfig, PrivacyLevel};
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
        client_id: config.discord().client_id.get().to_string(),
        client_secret: config.discord().client_secret.clone(),
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

#[derive(serde::Deserialize, Debug)]
#[serde(tag = "action", rename_all = "snake_case")]
pub enum DashViewRequest {
    Add {
        name: String,
        value: String,
    },
    Patch {
        id: String,
        name: Option<String>,
        value: Option<String>,
    },
    Remove {
        id: String,
    },
}

#[api_endpoint]
pub async fn dash_views(
    Extension(auth): Extension<AuthState>,
    State(ctx): State<ApiContext>,
    extract::Json(body): extract::Json<DashViewRequest>,
) -> Json<Value> {
    let Some(system_id) = auth.system_id() else {
        return Err(crate::error::GENERIC_AUTH_ERROR);
    };

    match body {
        DashViewRequest::Add { name, value } => {
            match sqlx::query_as::<Postgres, PKDashView>(
                "select * from dash_views where name = $1 and system = $2",
            )
            .bind(&name)
            .bind(system_id)
            .fetch_optional(&ctx.db)
            .await
            {
                Ok(val) => {
                    if val.is_some() {
                        return Err(crate::error::GENERIC_BAD_REQUEST);
                    };

                    match sqlx::query_as::<Postgres, PKDashView>(
                        "insert into dash_views (system, name, value) values ($1, $2, $3) returning *",
                    )
                    .bind(system_id)
                    .bind(name)
                    .bind(value)
                    .fetch_one(&ctx.db)
                    .await
                    {
                        Ok(res) => Ok(Json(res.to_json())),
                        Err(err) => fail!(?err, "failed to insert dash views"),
                    }
                }
                Err(err) => fail!(?err, "failed to query dash views"),
            }
        }
        DashViewRequest::Patch { id, name, value } => {
            match sqlx::query_as::<Postgres, PKDashView>(
                "select * from dash_views where id = $1 and system = $2",
            )
            .bind(id)
            .bind(system_id)
            .fetch_optional(&ctx.db)
            .await
            {
                Ok(val) => {
                    let Some(val) = val else {
                        return Err(crate::error::GENERIC_BAD_REQUEST);
                    };
                    // update
                    Ok(Json(Value::Null))
                }
                Err(err) => fail!(?err, "failed to query dash views"),
            }
        }
        DashViewRequest::Remove { id } => {
            match sqlx::query_as::<Postgres, PKDashView>(
                "select * from dash_views where id = $1 and system = $2",
            )
            .bind(id)
            .bind(system_id)
            .fetch_optional(&ctx.db)
            .await
            {
                Ok(val) => {
                    let Some(val) = val else {
                        return Err(crate::error::GENERIC_BAD_REQUEST);
                    };
                    match sqlx::query::<Postgres>(
                        "delete from dash_views where id = $1 and system = $2 returning *",
                    )
                    .bind(val.id)
                    .bind(system_id)
                    .fetch_one(&ctx.db)
                    .await
                    {
                        Ok(_) => Ok(Json(Value::Null)),
                        Err(err) => fail!(?err, "failed to remove dash views"),
                    }
                }
                Err(err) => fail!(?err, "failed to query dash views"),
            }
        }
    }
}

#[api_endpoint]
pub async fn dash_view(State(ctx): State<ApiContext>, Path(id): Path<String>) -> Json<Value> {
    match sqlx::query_as::<Postgres, PKDashView>("select * from dash_views where id = $1")
        .bind(id)
        .fetch_optional(&ctx.db)
        .await
    {
        Ok(val) => {
            let Some(val) = val else {
                return Err(crate::error::GENERIC_BAD_REQUEST);
            };
            Ok(Json(val.to_json()))
        }
        Err(err) => fail!(?err, "failed to query dash views"),
    }
}
