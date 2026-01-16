use api::{ApiContext, fail_html};
use askama::Template;
use axum::{
    extract::{MatchedPath, Request, State},
    http::header::SET_COOKIE,
    middleware::Next,
    response::{AppendHeaders, IntoResponse, Redirect, Response},
};
use axum_extra::extract::cookie::CookieJar;
use fred::{
    prelude::{KeysInterface, LuaInterface},
    util::sha1_hash,
};
use rand::{Rng, distributions::Alphanumeric};
use serde::{Deserialize, Serialize};

use crate::web::{message, render};

const LOGIN_TOKEN_TTL_SECS: i64 = 60 * 10;

const SESSION_LUA_SCRIPT: &str = r#"
local session_key = KEYS[1]
local ttl = ARGV[1]

local session_data = redis.call('GET', session_key)
if session_data then
    redis.call('EXPIRE', session_key, ttl)
end
return session_data
"#;

const SESSION_TTL_SECS: i64 = 60 * 60 * 4;

lazy_static::lazy_static! {
    static ref SESSION_LUA_SCRIPT_SHA: String = sha1_hash(SESSION_LUA_SCRIPT);
}

fn rand_token() -> String {
    rand::thread_rng()
        .sample_iter(&Alphanumeric)
        .take(64)
        .map(char::from)
        .collect()
}

#[derive(Clone, Serialize, Deserialize)]
pub struct AuthState {
    pub email: String,

    pub csrf_token: String,
    pub session_id: String,
}

impl AuthState {
    fn new(email: String) -> Self {
        Self {
            email,
            csrf_token: rand_token(),
            session_id: rand_token(),
        }
    }

    async fn from_request(
        headers: axum::http::HeaderMap,
        ctx: &ApiContext,
    ) -> anyhow::Result<Option<Self>> {
        let jar = CookieJar::from_headers(&headers);
        let Some(session_cookie) = jar.get("pk-session") else {
            return Ok(None);
        };
        let session_id = session_cookie.value();

        let session_key = format!("premium:session:{}", session_id);

        let script_exists: Vec<usize> = ctx
            .redis
            .script_exists(vec![SESSION_LUA_SCRIPT_SHA.to_string()])
            .await?;

        if script_exists[0] != 1 {
            ctx.redis
                .script_load::<String, String>(SESSION_LUA_SCRIPT.to_string())
                .await?;
        }

        let session_data: Option<String> = ctx
            .redis
            .evalsha(
                SESSION_LUA_SCRIPT_SHA.to_string(),
                vec![session_key],
                vec![SESSION_TTL_SECS],
            )
            .await?;

        let Some(session_data) = session_data else {
            return Ok(None);
        };

        let session: AuthState = serde_json::from_str(&session_data)?;
        Ok(Some(session))
    }

    async fn save(&self, ctx: &ApiContext) -> anyhow::Result<()> {
        let session_key = format!("premium:session:{}", self.session_id);
        let session_data = serde_json::to_string(&self)?;
        ctx.redis
            .set::<(), _, _>(
                session_key,
                session_data,
                Some(fred::types::Expiration::EX(SESSION_TTL_SECS)),
                None,
                false,
            )
            .await?;
        Ok(())
    }

    async fn delete(&self, ctx: &ApiContext) -> anyhow::Result<()> {
        let session_key = format!("premium:session:{}", self.session_id);
        ctx.redis.del::<(), _>(session_key).await?;
        Ok(())
    }
}

fn refresh_session_cookie(session: &AuthState, mut response: Response) -> Response {
    let cookie_value = format!(
        "pk-session={}; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age={}",
        session.session_id, SESSION_TTL_SECS
    );
    response
        .headers_mut()
        .insert(SET_COOKIE, cookie_value.parse().unwrap());
    response
}

pub async fn middleware(
    State(ctx): State<ApiContext>,
    mut request: Request,
    next: Next,
) -> Response {
    let extensions = request.extensions().clone();

    let endpoint = extensions
        .get::<MatchedPath>()
        .cloned()
        .map(|v| v.as_str().to_string())
        .unwrap_or("unknown".to_string());

    let session = match AuthState::from_request(request.headers().clone(), &ctx).await {
        Ok(s) => s,
        Err(err) => fail_html!(?err, "failed to fetch auth state from redis"),
    };

    if let Some(session) = session.clone() {
        request.extensions_mut().insert(session);
    }

    match endpoint.as_str() {
        "/" => {
            if let Some(ref session) = session {
                let response = next.run(request).await;
                refresh_session_cookie(session, response)
            } else {
                return render!(crate::web::Index {
                    base_url: libpk::config.premium().base_url.clone(),
                    session: None,
                    show_login_form: true,
                    message: None,
                    subscriptions: vec![],
                    paddle: None,
                });
            }
        }
        "/info/" => {
            let response = next.run(request).await;
            if let Some(ref session) = session {
                refresh_session_cookie(session, response)
            } else {
                response
            }
        }
        "/login" => {
            if let Some(ref session) = session {
                // no session here because that shows the "you're logged in as" component
                let response = render!(message("you are already logged in! go back home and log out if you need to log in to a different account.".to_string(), None));
                return refresh_session_cookie(session, response);
            } else {
                let body = match axum::body::to_bytes(request.into_body(), 1024 * 16).await {
                    Ok(b) => b,
                    Err(err) => fail_html!(?err, "failed to read request body"),
                };
                let form: std::collections::HashMap<String, String> =
                    match serde_urlencoded::from_bytes(&body) {
                        Ok(f) => f,
                        Err(err) => fail_html!(?err, "failed to parse form data"),
                    };
                let Some(email) = form.get("email") else {
                    return render!(crate::web::Index {
                        base_url: libpk::config.premium().base_url.clone(),
                        session: None,
                        show_login_form: true,
                        message: Some("email field is required".to_string()),
                        subscriptions: vec![],
                        paddle: None,
                    });
                };
                let email = email.trim().to_lowercase();
                if email.is_empty() {
                    return render!(crate::web::Index {
                        base_url: libpk::config.premium().base_url.clone(),
                        session: None,
                        show_login_form: true,
                        message: Some("email field is required".to_string()),
                        subscriptions: vec![],
                        paddle: None,
                    });
                }

                let token = rand_token();

                let token_key = format!("premium:login_token:{}", token);
                if let Err(err) = ctx
                    .redis
                    .set::<(), _, _>(
                        token_key,
                        &email,
                        Some(fred::types::Expiration::EX(LOGIN_TOKEN_TTL_SECS)),
                        None,
                        false,
                    )
                    .await
                {
                    fail_html!(?err, "failed to store login token in redis");
                }

                if let Err(err) = crate::mailer::login_token(email, token).await {
                    fail_html!(?err, "failed to send login email");
                }

                return render!(message(
                    "check your email for a login link! it will expire in 10 minutes.".to_string(),
                    None
                ));
            }
        }
        "/login/{token}" => {
            if let Some(ref session) = session {
                // no session here because that shows the "you're logged in as" component
                let response = render!(message("you are already logged in! go back home and log out if you need to log in to a different account.".to_string(), None));
                return refresh_session_cookie(session, response);
            }

            let path = request.uri().path();
            let token = path.strip_prefix("/login/").unwrap_or("");
            if token.is_empty() {
                return render!(crate::web::Index {
                    base_url: libpk::config.premium().base_url.clone(),
                    session: None,
                    show_login_form: true,
                    message: Some("invalid login link".to_string()),
                    subscriptions: vec![],
                    paddle: None,
                });
            }

            let token_key = format!("premium:login_token:{}", token);
            let email: Option<String> = match ctx.redis.get(&token_key).await {
                Ok(e) => e,
                Err(err) => fail_html!(?err, "failed to fetch login token from redis"),
            };

            let Some(email) = email else {
                return render!(crate::web::Index {
                    base_url: libpk::config.premium().base_url.clone(),
                    session: None,
                    show_login_form: true,
                    message: Some(
                        "invalid or expired login link. please request a new one.".to_string()
                    ),
                    subscriptions: vec![],
                    paddle: None,
                });
            };

            if let Err(err) = ctx.redis.del::<(), _>(&token_key).await {
                fail_html!(?err, "failed to delete login token from redis");
            }

            let session = AuthState::new(email);
            if let Err(err) = session.save(&ctx).await {
                fail_html!(?err, "failed to save session to redis");
            }

            let cookie_value = format!(
                "pk-session={}; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age={}",
                session.session_id, SESSION_TTL_SECS
            );
            (
                AppendHeaders([(SET_COOKIE, cookie_value)]),
                Redirect::to("/"),
            )
                .into_response()
        }
        "/logout" => {
            let Some(session) = session else {
                return Redirect::to("/").into_response();
            };

            let body = match axum::body::to_bytes(request.into_body(), 1024 * 16).await {
                Ok(b) => b,
                Err(err) => fail_html!(?err, "failed to read request body"),
            };
            let form: std::collections::HashMap<String, String> =
                match serde_urlencoded::from_bytes(&body) {
                    Ok(f) => f,
                    Err(err) => fail_html!(?err, "failed to parse form data"),
                };

            let csrf_valid = form
                .get("csrf_token")
                .map(|t| t == &session.csrf_token)
                .unwrap_or(false);

            if !csrf_valid {
                return (axum::http::StatusCode::FORBIDDEN, "invalid csrf token").into_response();
            }

            if let Err(err) = session.delete(&ctx).await {
                fail_html!(?err, "failed to delete session from redis");
            }

            let cookie_value = "pk-session=; Path=/; HttpOnly; Max-Age=0";
            (
                AppendHeaders([(SET_COOKIE, cookie_value)]),
                Redirect::to("/"),
            )
                .into_response()
        }
        "/cancel" | "/validate-token" => {
            if let Some(ref session) = session {
                let response = next.run(request).await;
                refresh_session_cookie(session, response)
            } else {
                Redirect::to("/").into_response()
            }
        }
        _ => (axum::http::StatusCode::NOT_FOUND, "404 not found").into_response(),
    }
}
