use std::time::{Duration, SystemTime};

use axum::{
    extract::State,
    http::Request,
    middleware::{FromFnLayer, Next},
    response::Response,
};
use fred::{pool::RedisPool, prelude::LuaInterface, types::ReconnectPolicy, util::sha1_hash};
use http::{HeaderValue, StatusCode};
use tracing::{error, info, warn};

use crate::util::{header_or_unknown, json_err};

const LUA_SCRIPT: &str = include_str!("ratelimit.lua");

lazy_static::lazy_static! {
    static ref LUA_SCRIPT_SHA: String = sha1_hash(LUA_SCRIPT);
}

// todo lol
const TOKEN2: &'static str = "h";

// this is awful but it works
pub fn ratelimiter<F, T>(f: F) -> FromFnLayer<F, Option<RedisPool>, T> {
    let redis = libpk::config.api.ratelimit_redis_addr.as_ref().map(|val| {
        let r = fred::pool::RedisPool::new(
            fred::types::RedisConfig::from_url_centralized(val.as_ref())
                .expect("redis url is invalid"),
            10,
        )
        .expect("failed to connect to redis");

        let handle = r.connect(Some(ReconnectPolicy::default()));

        tokio::spawn(async move { handle });

        let rscript = r.clone();
        tokio::spawn(async move {
            if let Ok(()) = rscript.wait_for_connect().await {
                match rscript.script_load(LUA_SCRIPT).await {
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

pub async fn do_request_ratelimited<B>(
    State(redis): State<Option<RedisPool>>,
    request: Request<B>,
    next: Next<B>,
) -> Response {
    if let Some(redis) = redis {
        let headers = request.headers().clone();
        let source_ip = header_or_unknown(headers.get("Fly-Client-IP"));

        let (rl_key, rate) = if let Some(header) = request.headers().clone().get("X-PluralKit-App")
        {
            if header == TOKEN2 {
                ("token2", 20)
            } else {
                (source_ip, 2)
            }
        } else {
            (source_ip, 2)
        };

        let burst = 5;
        let period = 1; // seconds

        // todo: make this static
        // though even if it's not static, it's probably cheaper than sending the entire script to redis every time
        let scriptsha = sha1_hash(&LUA_SCRIPT);

        // local rate_limit_key = KEYS[1]
        // local burst = ARGV[1]
        // local rate = ARGV[2]
        // local period = ARGV[3]
        // return {remaining, retry_after, reset_after}
        let resp = redis
            .evalsha::<(i32, String, u64), String, Vec<&str>, Vec<i32>>(
                scriptsha,
                vec![rl_key],
                vec![burst, rate, period],
            )
            .await;

        match resp {
            Ok((mut remaining, retry_after, reset_after)) => {
                let mut response = if remaining > 0 {
                    next.run(request).await
                } else {
                    json_err(
                        StatusCode::TOO_MANY_REQUESTS,
                        format!(
                            // todo: the retry_after is horribly wrong
                            r#"{{"message":"429: too many requests","retry_after":{retry_after}}}"#
                        ),
                    )
                };

                // the redis script puts burst in remaining for ??? some reason
                remaining -= burst - rate;

                let reset_time = SystemTime::now()
                    .checked_add(Duration::from_secs(reset_after))
                    .expect("invalid timestamp")
                    .duration_since(std::time::UNIX_EPOCH)
                    .expect("invalid duration")
                    .as_secs();

                let headers = response.headers_mut();
                headers.insert(
                    "X-RateLimit-Limit",
                    HeaderValue::from_str(format!("{}", rate).as_str())
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
