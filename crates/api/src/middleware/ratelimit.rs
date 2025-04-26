use std::time::{Duration, SystemTime};

use axum::{
    extract::{MatchedPath, Request, State},
    http::{HeaderValue, Method, StatusCode},
    middleware::{FromFnLayer, Next},
    response::Response,
};
use fred::{clients::RedisPool, interfaces::ClientLike, prelude::LuaInterface, util::sha1_hash};
use metrics::counter;
use tracing::{debug, error, info, warn};

use crate::util::{header_or_unknown, json_err};

use super::authnz::{INTERNAL_APPID_HEADER, INTERNAL_SYSTEMID_HEADER};

const LUA_SCRIPT: &str = include_str!("ratelimit.lua");

lazy_static::lazy_static! {
    static ref LUA_SCRIPT_SHA: String = sha1_hash(LUA_SCRIPT);
}

// this is awful but it works
pub fn ratelimiter<F, T>(f: F) -> FromFnLayer<F, Option<RedisPool>, T> {
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

            let rscript = r.clone();
            tokio::spawn(async move {
                if let Ok(()) = rscript.wait_for_connect().await {
                    match rscript
                        .script_load::<String, String>(LUA_SCRIPT.to_string())
                        .await
                    {
                        Ok(_) => info!("connected to redis for request rate limiting"),
                        Err(err) => error!("could not load redis script: {}", err),
                    }
                } else {
                    error!("could not wait for connection to load redis script!");
                }
            });

            r
        });

    if redis.is_none() {
        warn!("running without request rate limiting!");
    }

    axum::middleware::from_fn_with_state(redis, f)
}

enum RatelimitType {
    GenericGet,
    GenericUpdate,
    Message,
    TempCustom,
}

impl RatelimitType {
    fn key(&self) -> String {
        match self {
            RatelimitType::GenericGet => "generic_get",
            RatelimitType::GenericUpdate => "generic_update",
            RatelimitType::Message => "message",
            RatelimitType::TempCustom => "token2", // this should be "app_custom" or something
        }
        .to_string()
    }

    fn rate(&self) -> i32 {
        match self {
            RatelimitType::GenericGet => 10,
            RatelimitType::GenericUpdate => 3,
            RatelimitType::Message => 10,
            RatelimitType::TempCustom => 20,
        }
    }
}

pub async fn do_request_ratelimited(
    State(redis): State<Option<RedisPool>>,
    request: Request,
    next: Next,
) -> Response {
    if let Some(redis) = redis {
        let headers = request.headers().clone();
        let source_ip = header_or_unknown(headers.get("X-PluralKit-Client-IP"));
        let authenticated_system_id = header_or_unknown(headers.get(INTERNAL_SYSTEMID_HEADER));
        let authenticated_app_id = header_or_unknown(headers.get(INTERNAL_APPID_HEADER));

        let endpoint = request
            .extensions()
            .get::<MatchedPath>()
            .cloned()
            .map(|v| v.as_str().to_string())
            .unwrap_or("unknown".to_string());

        // looks like this chooses the tokens/sec by app_id or endpoint
        // then chooses the key by system_id or source_ip
        // todo: key should probably be chosen by app_id when it's present
        // todo: make x-ratelimit-scope actually meaningful

        // hack: for now, we only have one "registered app", so we hardcode the app id
        let rlimit = if authenticated_app_id == "1" {
            RatelimitType::TempCustom
        } else if endpoint == "/v2/messages/:message_id" {
            RatelimitType::Message
        } else if request.method() == Method::GET {
            RatelimitType::GenericGet
        } else {
            RatelimitType::GenericUpdate
        };

        let rl_key = format!(
            "{}:{}",
            if authenticated_system_id != "unknown"
                && matches!(rlimit, RatelimitType::GenericUpdate)
            {
                authenticated_system_id
            } else {
                source_ip
            },
            rlimit.key()
        );

        let period = 1; // seconds
        let cost = 1; // todo: update this for group member endpoints

        // local rate_limit_key = KEYS[1]
        // local rate = ARGV[1]
        // local period = ARGV[2]
        // return {remaining, tostring(retry_after), reset_after}

        // todo: check if error is script not found and reload script
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
            Err(err) => {
                tracing::error!("error getting ratelimit info: {}", err);
                return json_err(
                    StatusCode::INTERNAL_SERVER_ERROR,
                    r#"{"message": "500: internal server error", "code": 0}"#.to_string(),
                );
            }
        }
    }

    next.run(request).await
}
