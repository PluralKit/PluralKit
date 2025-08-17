use std::time::{Duration, SystemTime};

use axum::{
    extract::{MatchedPath, Request, State},
    http::{HeaderValue, Method, StatusCode},
    middleware::{FromFnLayer, Next},
    response::Response,
};
use fred::{clients::RedisPool, interfaces::ClientLike, prelude::LuaInterface, util::sha1_hash};
use metrics::counter;
use sqlx::Postgres;
use tracing::{debug, error, info, warn};

use crate::{
    ApiContext,
    auth::AuthState,
    util::{header_or_unknown, json_err},
};
use pluralkit_models::PKExternalApp;

const LUA_SCRIPT: &str = include_str!("ratelimit.lua");

lazy_static::lazy_static! {
    static ref LUA_SCRIPT_SHA: String = sha1_hash(LUA_SCRIPT);
}

// this is awful but it works
pub fn ratelimiter<F, T>(
    ctx: ApiContext,
    f: F,
) -> FromFnLayer<F, (ApiContext, Option<RedisPool>), T> {
    let redis = libpk::config
        .api
        .as_ref()
        .expect("missing api config")
        .ratelimit_redis_addr
        .as_ref()
        .map(|val| {
            // todo: this should probably use the global pool
            let r = RedisPool::new(
                fred::types::RedisConfig::from_url_centralized(val.as_ref())
                    .expect("redis url is invalid"),
                None,
                None,
                Some(Default::default()),
                10,
            )
            .expect("failed to connect to redis");

            let handle = r.connect();

            tokio::spawn(async move { handle });

            r
        });

    if redis.is_none() {
        warn!("running without request rate limiting!");
    }

    axum::middleware::from_fn_with_state((ctx, redis), f)
}

enum RatelimitType {
    GenericGet,
    GenericUpdate,
    Message,
    AppCustom(i32),
}

impl RatelimitType {
    fn key(&self) -> String {
        match self {
            RatelimitType::GenericGet => "generic_get",
            RatelimitType::GenericUpdate => "generic_update",
            RatelimitType::Message => "message",
            RatelimitType::AppCustom(_) => "app_custom",
        }
        .to_string()
    }

    fn rate(&self) -> i32 {
        match self {
            RatelimitType::GenericGet => 10,
            RatelimitType::GenericUpdate => 3,
            RatelimitType::Message => 10,
            RatelimitType::AppCustom(n) => *n,
        }
    }
}

pub async fn do_request_ratelimited(
    State((ctx, redis)): State<(ApiContext, Option<RedisPool>)>,
    request: Request,
    next: Next,
) -> Response {
    if let Some(redis) = redis {
        let headers = request.headers().clone();
        if headers.get("x-pluralkit-internal").is_some() {
            // bypass ratelimiting entirely for internal requests
            return next.run(request).await;
        }

        let extensions = request.extensions().clone();
        let source_ip = header_or_unknown(headers.get("X-PluralKit-Client-IP"));

        let mut app_rate: Option<i32> = None;
        if let Some(app_header) = request.headers().clone().get("x-pluralkit-app") {
            let app_token = app_header.to_str().unwrap_or("invalid");
            if app_token.starts_with("pkap2:")
                && let Some(app) = sqlx::query_as::<Postgres, PKExternalApp>(
                    "select * from external_apps where api_rl_token = $1",
                )
                .bind(&app_token[6..])
                .fetch_optional(&ctx.db)
                .await
                .expect("failed to query external app in postgres")
            {
                app_rate = Some(app.api_rl_rate.expect("external app has no api_rl_rate"));
            }
        };

        let endpoint = extensions
            .get::<MatchedPath>()
            .cloned()
            .map(|v| v.as_str().to_string())
            .unwrap_or("unknown".to_string());

        let auth = extensions
            .get::<AuthState>()
            .expect("should always have AuthState");

        // looks like this chooses the tokens/sec by app_id or endpoint
        // then chooses the key by system_id or source_ip
        // todo: key should probably be chosen by app_id when it's present
        // todo: make x-ratelimit-scope actually meaningful

        let rlimit = if let Some(r) = app_rate {
            RatelimitType::AppCustom(r)
        } else if endpoint == "/v2/messages/:message_id" {
            RatelimitType::Message
        } else if request.method() == Method::GET {
            RatelimitType::GenericGet
        } else {
            RatelimitType::GenericUpdate
        };

        let rl_key = format!(
            "{}:{}",
            if let Some(system_id) = auth.system_id()
                && matches!(rlimit, RatelimitType::GenericUpdate)
            {
                system_id.to_string()
            } else {
                source_ip.to_string()
            },
            rlimit.key()
        );

        let period = 1; // seconds
        let cost = 1; // todo: update this for group member endpoints

        let script_exists: Vec<usize> =
            match redis.script_exists(vec![LUA_SCRIPT_SHA.to_string()]).await {
                Ok(exists) => exists,
                Err(error) => {
                    error!(?error, "failed to check ratelimit script");
                    return json_err(
                        StatusCode::INTERNAL_SERVER_ERROR,
                        r#"{"message": "500: internal server error", "code": 0}"#.to_string(),
                    );
                }
            };

        if script_exists[0] != 1 {
            match redis
                .script_load::<String, String>(LUA_SCRIPT.to_string())
                .await
            {
                Ok(_) => info!("successfully loaded ratelimit script to redis"),
                Err(error) => {
                    error!(?error, "could not load redis script")
                }
            }
        }

        // local rate_limit_key = KEYS[1]
        // local rate = ARGV[1]
        // local period = ARGV[2]
        // return {remaining, tostring(retry_after), reset_after}
        let resp = redis
            .evalsha::<(i32, String, u64), String, Vec<String>, Vec<i32>>(
                LUA_SCRIPT_SHA.to_string(),
                vec![rl_key.clone()],
                vec![rlimit.rate(), period, cost],
            )
            .await;

        match resp {
            Ok((remaining, retry_after, reset_after)) => {
                // redis's lua doesn't support returning floats
                let retry_after: f64 = retry_after
                    .parse()
                    .expect("got something that isn't a f64 from redis");

                let mut response = if remaining > 0 {
                    next.run(request).await
                } else {
                    let retry_after = (retry_after * 1_000_f64).ceil() as u64;
                    debug!("ratelimited request from {rl_key}, retry_after={retry_after}",);
                    counter!("pk_http_requests_ratelimited").increment(1);
                    json_err(
                        StatusCode::TOO_MANY_REQUESTS,
                        format!(
                            r#"{{"message":"429: too many requests","retry_after":{retry_after},"scope":"{}","code":0}}"#,
                            rlimit.key(),
                        ),
                    )
                };

                let reset_time = SystemTime::now()
                    .checked_add(Duration::from_secs(reset_after))
                    .expect("invalid timestamp")
                    .duration_since(std::time::UNIX_EPOCH)
                    .expect("invalid duration")
                    .as_secs();

                let headers = response.headers_mut();
                headers.insert(
                    "X-RateLimit-Scope",
                    HeaderValue::from_str(rlimit.key().as_str()).expect("invalid header value"),
                );
                headers.insert(
                    "X-RateLimit-Limit",
                    HeaderValue::from_str(format!("{}", rlimit.rate()).as_str())
                        .expect("invalid header value"),
                );
                headers.insert(
                    "X-RateLimit-Remaining",
                    HeaderValue::from_str(format!("{}", remaining).as_str())
                        .expect("invalid header value"),
                );
                headers.insert(
                    "X-RateLimit-Reset",
                    HeaderValue::from_str(format!("{}", reset_time).as_str())
                        .expect("invalid header value"),
                );

                return response;
            }
            Err(error) => {
                error!(?error, "error getting ratelimit info");
                return json_err(
                    StatusCode::INTERNAL_SERVER_ERROR,
                    r#"{"message": "500: internal server error", "code": 0}"#.to_string(),
                );
            }
        }
    }

    next.run(request).await
}
