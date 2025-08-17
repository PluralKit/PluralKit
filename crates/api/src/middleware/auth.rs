use axum::{
    extract::{Request, State, MatchedPath},
    http::StatusCode,
    middleware::Next,
    response::Response,
};

use uuid::Uuid;
use subtle::ConstantTimeEq;

use tracing::error;
use sqlx::Postgres;

use pluralkit_models::{ApiKeyType, PKApiKey};
use crate::auth::{AccessLevel, AuthState};
use crate::{util::json_err, ApiContext};

pub fn is_part_path<'a, 'b>(part: &'a str, endpoint: &'b str) -> bool {
    if !endpoint.starts_with("/v2/") {
        return false;
    }

    let path_frags = endpoint[4..].split("/").collect::<Vec<&str>>();
    match part {
        "system" => match &path_frags[..] {
            ["systems", _] => true,
            ["systems", _, "settings"] => true,
            ["systems", _, "autoproxy"] => true,
            ["systems", _, "guilds", ..] => true,
            _ => false,
        },
        "members" => match &path_frags[..] {
            ["systems", _, "members"] => true,
            ["members"] => true,
            ["members", _, "groups"] => false,
            ["members", _, "groups", ..] => false,
            ["members", ..] => true,
            _ => false,
        },
        "groups" => match &path_frags[..] {
            ["systems", _, "groups"] => true,
            ["groups"] => true,
            ["groups", ..] => true,
            ["members", _, "groups"] => true,
            ["members", _, "groups", ..] => true,
            _ => false,
        },
        "fronters" => match &path_frags[..] {
            ["systems", _, "fronters"] => true,
            _ => false,
        },
        "switches" => match &path_frags[..] {
            // switches implies fronters
            ["systems", _, "fronters"] => true,
            ["systems", _, "switches"] => true,
            ["systems", _, "switches", ..] => true,
            _ => false,
        },
        _ => false,
    }
}

pub fn apikey_can_access(token: &PKApiKey, method: String, endpoint: String) -> AccessLevel {
    if token.kind == ApiKeyType::Dashboard {
        return AccessLevel::Full;
    }

    let mut access = AccessLevel::None;
    for rscope in token.scopes.iter() {
        let scope = rscope.split(":").collect::<Vec<&str>>();
        let na = match (method.as_str(), &scope[..]) {
            ("GET", ["identify"]) => {
                if &endpoint == "/v2/systems/:system_id" {
                    AccessLevel::PublicRead
                } else {
                    AccessLevel::None
                }
            }

            ("GET", ["publicread", part]) => {
                if *part == "all" || is_part_path(part.as_ref(), endpoint.as_ref()) {
                    AccessLevel::PublicRead
                } else {
                    AccessLevel::None
                }
            }

            ("GET", ["read", part]) => {
                if *part == "all" || is_part_path(part.as_ref(), endpoint.as_ref()) {
                    AccessLevel::PrivateRead
                } else {
                    AccessLevel::None
                }
            }

            (_, ["write", part]) => {
                if *part == "all" || is_part_path(part.as_ref(), endpoint.as_ref()) {
                    AccessLevel::Full
                } else {
                    AccessLevel::None
                }
            }

            _ => AccessLevel::None,
        };

        if na > access {
            access = na;
        }
    }

    access
}

pub async fn auth(State(ctx): State<ApiContext>, mut req: Request, next: Next) -> Response {
	let endpoint = req
        .extensions()
        .get::<MatchedPath>()
        .cloned()
        .map(|v| v.as_str().to_string())
        .unwrap_or("unknown".to_string());

    let mut authed_system_id: Option<i32> = None;
    let mut authed_app_id: Option<Uuid> = None;
	let mut authed_api_key_id: Option<Uuid> = None;
	let mut access_level = AccessLevel::None;

    // fetch user authorization
    if let Some(system_auth_header) = req
        .headers()
        .get("authorization")
        .map(|h| h.to_str().ok())
        .flatten()
	{
		if system_auth_header.starts_with("Bearer ")
            && let Some(tid) =
                PKApiKey::parse_header_str(system_auth_header[7..].to_string(), &ctx.token_publickey)
            && let Some(token) =
                sqlx::query_as::<Postgres, PKApiKey>("select * from api_keys where id = $1")
                    .bind(&tid)
                    .fetch_optional(&ctx.db)
                    .await
                    .expect("failed to query apitoken in postgres")
        {
			authed_api_key_id = Some(tid);
			access_level = apikey_can_access(&token, req.method().to_string(), endpoint.clone());
			if access_level != AccessLevel::None {
				authed_system_id = Some(token.system);
			}
		}
		else if let Some(system_id) =
            match libpk::db::repository::legacy_token_auth(&ctx.db, system_auth_header).await {
                Ok(val) => val,
                Err(err) => {
                    error!(?err, "failed to query authorization token in postgres");
                    return json_err(
                        StatusCode::INTERNAL_SERVER_ERROR,
                        r#"{"message": "500: Internal Server Error", "code": 0}"#.to_string(),
                    );
                }
            }
		{
			authed_system_id = Some(system_id);
			access_level = AccessLevel::Full;
		}
	}

    // fetch app authorization
    if let Some(app_auth_header) = req
        .headers()
        .get("x-pluralkit-app")
        .map(|h| h.to_str().ok())
        .flatten()
        && let Some(app_id) =
			match libpk::db::repository::app_token_auth(&ctx.db, app_auth_header).await {
                Ok(val) => val,
                Err(err) => {
                    error!(?err, "failed to query authorization token in postgres");
                    return json_err(
                        StatusCode::INTERNAL_SERVER_ERROR,
                        r#"{"message": "500: Internal Server Error", "code": 0}"#.to_string(),
                    );
                }
            }
    {
        authed_app_id = Some(app_id);
    }

    // todo: fix syntax
    let internal = if req.headers().get("x-pluralkit-client-ip").is_none()
        && let Some(auth_header) = req
            .headers()
            .get("x-pluralkit-internalauth")
            .map(|h| h.to_str().ok())
            .flatten()
        && let Some(real_token) = libpk::config.internal_auth.clone()
        && auth_header.as_bytes().ct_eq(real_token.as_bytes()).into()
    {
        true
    } else {
        false
    };

    req.extensions_mut()
        .insert(AuthState::new(authed_system_id, authed_app_id, authed_api_key_id, access_level, internal));

    next.run(req).await
}
