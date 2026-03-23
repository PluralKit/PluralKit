use axum::{
    extract::{Request, State},
    http::StatusCode,
    middleware::Next,
    response::Response,
    routing::url_params::UrlParams,
};

use sqlx::{Postgres, prelude::FromRow, types::Uuid};
use tracing::error;

use crate::auth::AuthState;
use crate::{ApiContext, util::json_err};
use pluralkit_models::SystemId;

/// The system about which the current request is
#[derive(Clone, FromRow)]
pub struct RequestAboutSystem {
    pub id: SystemId,
}

// move this somewhere else
fn parse_hid(hid: &str) -> String {
    if hid.len() > 7 || hid.len() < 5 {
        hid.to_string()
    } else {
        hid.to_lowercase().replace("-", "")
    }
}

pub async fn params(State(ctx): State<ApiContext>, mut req: Request, next: Next) -> Response {
    let pms = match req.extensions().get::<UrlParams>() {
        None => Vec::new(),
        Some(UrlParams::Params(pms)) => pms.clone(),
        _ => {
            return json_err(
                StatusCode::BAD_REQUEST,
                r#"{"message":"400: Bad Request","code": 0}"#.to_string(),
            )
            .into();
        }
    };

    for (key, value) in pms {
        match key.as_ref() {
            "system_id" => match value.as_str() {
                "@me" => {
                    let Some(system_id) = req
                        .extensions()
                        .get::<AuthState>()
                        .expect("missing auth state")
                        .system_id()
                    else {
                        return json_err(
                            StatusCode::UNAUTHORIZED,
                            r#"{"message":"401: Missing or invalid Authorization header","code": 0}"#.to_string(),
                        )
                        .into();
                    };

                    req.extensions_mut()
                        .insert(RequestAboutSystem { id: system_id });
                }
                id => {
                    println!("params.rs. lookup {id}");
                    let query = match Uuid::parse_str(id) {
                        Ok(uuid) => sqlx::query_as::<Postgres, RequestAboutSystem>(
                            "select id from systems where uuid = $1",
                        )
                        .bind(uuid),
                        Err(_) => match id.parse::<i64>() {
                            Ok(parsed) => sqlx::query_as::<Postgres, RequestAboutSystem>(
                                "select id from systems where id = (select system from accounts where uid = $1)"
                            )
                            .bind(parsed),
                            Err(_) => sqlx::query_as::<Postgres, RequestAboutSystem>(
                                "select id from systems where hid = $1",
                            )
                            .bind(parse_hid(id))
                        },
                    };
                    match query.fetch_optional(&ctx.db).await {
                        Ok(Some(request_system)) => {
                            req.extensions_mut().insert(request_system);
                        }
                        Ok(None) => {
                            return json_err(
                                StatusCode::NOT_FOUND,
                                r#"{"message":"System not found.","code":20001}"#.to_string(),
                            );
                        }
                        Err(err) => {
                            error!(?err, ?id, "failed to query system from path in db");
                            return json_err(
                                StatusCode::INTERNAL_SERVER_ERROR,
                                r#"{"message": "500: Internal Server Error", "code": 0}"#
                                    .to_string(),
                            );
                        }
                    }
                }
            },
            "member_id" => {}
            "group_id" => {}
            "switch_id" => {}
            "guild_id" => {}
            _ => {}
        }
    }

    next.run(req).await
}
