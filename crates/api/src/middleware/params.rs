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

type MemberId = i32;
type GroupId = i32;
type SwitchId = i32;

#[derive(Clone, FromRow)]
pub struct RequestAboutSystem {
    pub id: SystemId,
}

#[derive(Clone, FromRow)]
pub struct RequestAboutMember {
    pub id: MemberId,
    pub system: SystemId,
}

#[derive(Clone, FromRow)]
pub struct RequestAboutGroup {
    pub id: GroupId,
    pub system: SystemId,
}

#[derive(Clone, FromRow)]
pub struct RequestAboutSwitch {
    pub id: SwitchId,
    pub system: SystemId,
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
                system_ref => {
                    println!("params.rs. lookup systemRef {system_ref}");
                    match lookup_system_id_from_system_ref(system_ref, &ctx.db).await {
                        Ok(requested_system) => {
                            req.extensions_mut().insert(requested_system);
                        }
                        Err(fail_fast_response) => return fail_fast_response,
                    }
                }
            },
            "member_id" => {
                let member_ref = value.as_str();
                println!("params.rs. lookup memberRef {member_ref}");
                match lookup_system_from_member_ref(member_ref, &ctx.db).await {
                    Ok(req_member) => {
                        req.extensions_mut().insert(req_member.clone());
                        req.extensions_mut().insert(RequestAboutSystem {
                            id: req_member.system,
                        });
                    }
                    Err(fail_fast_response) => return fail_fast_response,
                }
            }
            "group_id" => {
                let group_ref = value.as_str();
                println!("params.rs. lookup groupRef {group_ref}");
                match lookup_system_from_group_ref(group_ref, &ctx.db).await {
                    Ok(req_group) => {
                        req.extensions_mut().insert(req_group.clone());
                        req.extensions_mut().insert(RequestAboutSystem {
                            id: req_group.system,
                        });
                    }
                    Err(fail_fast_response) => return fail_fast_response,
                }
            }
            "switch_id" => {
                let switch_id = value.as_str();
                println!("params.rs. lookup switch_id {switch_id}");
                match lookup_system_from_switch_id(switch_id, &ctx.db).await {
                    Ok(req_switch) => {
                        req.extensions_mut().insert(req_switch.clone());
                        req.extensions_mut().insert(RequestAboutSystem {
                            id: req_switch.system,
                        });
                    }
                    Err(fail_fast_response) => return fail_fast_response,
                }
            }
            "guild_id" => {}
            _ => {}
        }
    }

    next.run(req).await
}

async fn lookup_system_id_from_system_ref(
    system_ref: &str,
    db: &sqlx::Pool<Postgres>,
) -> Result<RequestAboutSystem, Response> {
    let query = match Uuid::parse_str(system_ref) {
        Ok(uuid) => {
            sqlx::query_as::<Postgres, RequestAboutSystem>("select id from systems where uuid = $1")
                .bind(uuid)
        }
        Err(_) => match system_ref.parse::<i64>() {
            Ok(parsed) => sqlx::query_as::<Postgres, RequestAboutSystem>(
                "select id from systems where id = (select system from accounts where uid = $1)",
            )
            .bind(parsed),
            Err(_) => sqlx::query_as::<Postgres, RequestAboutSystem>(
                "select id from systems where hid = $1",
            )
            .bind(parse_hid(system_ref)),
        },
    };
    match query.fetch_optional(db).await {
        Ok(Some(request_system)) => Ok(request_system),
        Ok(None) => Err(json_err(
            StatusCode::NOT_FOUND,
            r#"{"message":"System not found.","code":20001}"#.to_string(),
        )),
        Err(err) => {
            error!(?err, ?system_ref, "failed to query system from path in db");
            Err(json_err(
                StatusCode::INTERNAL_SERVER_ERROR,
                r#"{"message": "500: Internal Server Error", "code": 0}"#.to_string(),
            ))
        }
    }
}

async fn lookup_system_from_member_ref(
    member_ref: &str,
    db: &sqlx::Pool<Postgres>,
) -> Result<RequestAboutMember, Response> {
    let query = match Uuid::parse_str(member_ref) {
        Ok(uuid) => sqlx::query_as::<Postgres, RequestAboutMember>(
            "select id, system from members where uuid = $1",
        )
        .bind(uuid),
        Err(_) => sqlx::query_as::<Postgres, RequestAboutMember>(
            "select id, system from members where hid = $1",
        )
        .bind(parse_hid(member_ref)),
    };

    match query.fetch_optional(db).await {
        Ok(Some(member)) => Ok(member),
        Ok(None) => Err(json_err(
            StatusCode::NOT_FOUND,
            r#"{"message":"Member not found.","code":20002}"#.to_string(),
        )),
        Err(err) => {
            error!(?err, ?member_ref, "failed to query member from path in db");
            Err(json_err(
                StatusCode::INTERNAL_SERVER_ERROR,
                r#"{"message": "500: Internal Server Error", "code": 0}"#.to_string(),
            ))
        }
    }
}

async fn lookup_system_from_group_ref(
    group_ref: &str,
    db: &sqlx::Pool<Postgres>,
) -> Result<RequestAboutGroup, Response> {
    let query = match Uuid::parse_str(group_ref) {
        Ok(uuid) => sqlx::query_as::<Postgres, RequestAboutGroup>(
            "select id, system from groups where uuid = $1",
        )
        .bind(uuid),
        Err(_) => sqlx::query_as::<Postgres, RequestAboutGroup>(
            "select id, system from groups where hid = $1",
        )
        .bind(parse_hid(group_ref)),
    };

    match query.fetch_optional(db).await {
        Ok(Some(group)) => Ok(group),
        Ok(None) => Err(json_err(
            StatusCode::NOT_FOUND,
            r#"{"message":"Group not found.","code":20004}"#.to_string(),
        )),
        Err(err) => {
            error!(?err, ?group_ref, "failed to query group from path in db");
            Err(json_err(
                StatusCode::INTERNAL_SERVER_ERROR,
                r#"{"message": "500: Internal Server Error", "code": 0}"#.to_string(),
            ))
        }
    }
}

async fn lookup_system_from_switch_id(
    switch_id: &str,
    db: &sqlx::Pool<Postgres>,
) -> Result<RequestAboutSwitch, Response> {
    let query = match Uuid::parse_str(switch_id) {
        Ok(uuid) => sqlx::query_as::<Postgres, RequestAboutSwitch>(
            "select id, system from switches where uuid = $1",
        )
        .bind(uuid),
        Err(_) => {
            return Err(json_err(
                StatusCode::BAD_REQUEST,
                r#"{"message":"400: Bad Request","code": 0}"#.to_string(),
            ));
        }
    };

    match query.fetch_optional(db).await {
        Ok(Some(switch)) => Ok(switch),
        Ok(None) => Err(json_err(
            StatusCode::NOT_FOUND,
            r#"{"message":"Switch not found.","code":20007}"#.to_string(),
        )),
        Err(err) => {
            error!(?err, ?switch_id, "failed to query switch from path in db");
            Err(json_err(
                StatusCode::INTERNAL_SERVER_ERROR,
                r#"{"message": "500: Internal Server Error", "code": 0}"#.to_string(),
            ))
        }
    }
}
