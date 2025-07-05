use axum::{
    extract::{Request, State},
    http::StatusCode,
    middleware::Next,
    response::Response,
    routing::url_params::UrlParams,
};

use sqlx::{types::Uuid, Postgres};
use tracing::error;

use crate::auth::AuthState;
use crate::{util::json_err, ApiContext};
use pluralkit_models::PKSystem;

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
            .into()
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

                    match sqlx::query_as::<Postgres, PKSystem>(
                        "select * from systems where id = $1",
                    )
                    .bind(system_id)
                    .fetch_optional(&ctx.db)
                    .await
                    {
                        Ok(Some(system)) => {
                            req.extensions_mut().insert(system);
                        }
                        Ok(None) => {
                            error!(
                                ?system_id,
                                "could not find previously authenticated system in db"
                            );
                            return json_err(
                                StatusCode::INTERNAL_SERVER_ERROR,
                                r#"{"message": "500: Internal Server Error", "code": 0}"#
                                    .to_string(),
                            );
                        }
                        Err(err) => {
                            error!(
                                ?err,
                                "failed to query previously authenticated system in db"
                            );
                            return json_err(
                                StatusCode::INTERNAL_SERVER_ERROR,
                                r#"{"message": "500: Internal Server Error", "code": 0}"#
                                    .to_string(),
                            );
                        }
                    }
                }
                id => {
                    println!("a {id}");
                    match match Uuid::parse_str(id) {
                        Ok(uuid) => sqlx::query_as::<Postgres, PKSystem>(
                            "select * from systems where uuid = $1",
                        )
                        .bind(uuid),
                        Err(_) => match id.parse::<i64>() {
                            Ok(parsed) => sqlx::query_as::<Postgres, PKSystem>(
                                "select * from systems where id = (select system from accounts where uid = $1)"
                            )
                            .bind(parsed),
                            Err(_) => sqlx::query_as::<Postgres, PKSystem>(
                                "select * from systems where hid = $1",
                            )
                            .bind(parse_hid(id))
                        },
                    }
                    .fetch_optional(&ctx.db)
                    .await
                    {
                        Ok(Some(system)) => {
                            req.extensions_mut().insert(system);
                        }
                        Ok(None) => {
                            return json_err(
                                StatusCode::NOT_FOUND,
                                r#"{"message":"System not found.","code":20001}"#.to_string(),
                            )
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
