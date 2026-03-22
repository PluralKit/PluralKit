use axum::{
    extract::{Request, State},
    http::StatusCode,
    middleware::Next,
    response::{IntoResponse, Response},
    routing::url_params::UrlParams,
};
use sqlx::{Postgres, types::Uuid};
use tracing::{error, info};

use crate::{
    ApiContext,
    auth::AuthState,
    error::{
        GENERIC_BAD_REQUEST, GENERIC_SERVER_ERROR, GROUP_NOT_FOUND, MEMBER_NOT_FOUND, PKError,
        SWITCH_NOT_FOUND, SYSTEM_NOT_FOUND,
    },
    util::json_err,
};
use pluralkit_models::{GroupId, MemberId, SwitchId, SystemId};

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
        let id_ref = value.as_str();
        match key.as_ref() {
            "system_id" if id_ref == "@me" => {
                let system_id = match req
                    .extensions()
                    .get::<AuthState>()
                    .expect("missing auth state")
                    .system_id()
                {
                    Some(system_id) => system_id,
                    _ => {
                        return json_err(
                                        StatusCode::UNAUTHORIZED,
                                        r#"{"message":"401: Missing or invalid Authorization header","code": 0}"#
                                            .to_string(),
                                    )
                                    .into();
                    }
                };

                req.extensions_mut()
                    .insert(RequestAboutSystem { id: system_id });
            }
            "system_id" => match resolve_system_id(id_ref, &ctx.db).await {
                Ok(system_id) => {
                    req.extensions_mut()
                        .insert(RequestAboutSystem { id: system_id });
                }
                Err(e) => return e.into_response(),
            },
            "member_id" => match resolve_member_id(id_ref, &ctx.db).await {
                Ok((member_id, system_id)) => {
                    req.extensions_mut().insert(RequestAboutMember {
                        id: member_id,
                        system: system_id,
                    });
                    req.extensions_mut()
                        .insert(RequestAboutSystem { id: system_id });
                }
                Err(e) => return e.into_response(),
            },
            "group_id" => match resolve_group_id(id_ref, &ctx.db).await {
                Ok((group_id, system_id)) => {
                    req.extensions_mut().insert(RequestAboutGroup {
                        id: group_id,
                        system: system_id,
                    });
                    req.extensions_mut()
                        .insert(RequestAboutSystem { id: system_id });
                }
                Err(e) => return e.into_response(),
            },
            "switch_id" => match resolve_switch_id(id_ref, &ctx.db).await {
                Ok((switch_id, system_id)) => {
                    req.extensions_mut().insert(RequestAboutSwitch {
                        id: switch_id,
                        system: system_id,
                    });
                    req.extensions_mut()
                        .insert(RequestAboutSystem { id: system_id });
                }
                Err(e) => return e.into_response(),
            },
            _ => {}
        }
    }

    next.run(req).await
}

// resolve and lookup owning system from reference

async fn resolve_system_id(id_ref: &str, db: &sqlx::Pool<Postgres>) -> Result<SystemId, PKError> {
    let system_ref = SystemRef::from(id_ref);
    info!(?system_ref, ?id_ref, "resolving system id");

    // use (SystemId,) instread of SystemId here, as otherwise sqlx doesn't find the postgres trait impl
    let row: Option<(SystemId,)> = match system_ref {
        SystemRef::Uuid(u) => sqlx::query_as("select id from systems where uuid = $1")
            .bind(u)
            .fetch_optional(db)
            .await
            .map_err(|err| {
                error!(?err, ?id_ref, "failed to query system by uuid from db");
                GENERIC_SERVER_ERROR
            })?,
        SystemRef::DiscordUid(uid) => sqlx::query_as("select system from accounts where uid = $1")
            .bind(uid)
            .fetch_optional(db)
            .await
            .map_err(|err| {
                error!(
                    ?err,
                    ?id_ref,
                    "failed to query system by discord uid from db"
                );
                GENERIC_SERVER_ERROR
            })?,
        SystemRef::Hid(hid) => sqlx::query_as("select id from systems where hid = $1")
            .bind(&hid)
            .fetch_optional(db)
            .await
            .map_err(|err| {
                error!(?err, ?id_ref, "failed to query system by hid from db");
                GENERIC_SERVER_ERROR
            })?,
    };

    row.map(|id| id.0).ok_or(SYSTEM_NOT_FOUND)
}

async fn resolve_member_id(
    id_ref: &str,
    db: &sqlx::Pool<Postgres>,
) -> Result<(MemberId, SystemId), PKError> {
    let member_ref = if let Ok(uuid) = Uuid::parse_str(id_ref) {
        MemberRef::Uuid(uuid)
    } else {
        MemberRef::Hid(parse_hid(id_ref))
    };
    info!(?member_ref, ?id_ref, "resolving member id");

    let row = match member_ref {
        MemberRef::Uuid(u) => sqlx::query_as("select id, system from members where uuid = $1")
            .bind(u)
            .fetch_optional(db)
            .await
            .map_err(|err| {
                error!(?err, ?id_ref, "failed to query member by uuid from db");
                GENERIC_SERVER_ERROR
            })?,
        MemberRef::Hid(hid) => sqlx::query_as("select id, system from members where hid = $1")
            .bind(hid)
            .fetch_optional(db)
            .await
            .map_err(|err| {
                error!(?err, ?id_ref, "failed to query member by hid from db");
                GENERIC_SERVER_ERROR
            })?,
    };

    row.ok_or(MEMBER_NOT_FOUND)
}

async fn resolve_group_id(
    id_ref: &str,
    db: &sqlx::Pool<Postgres>,
) -> Result<(GroupId, SystemId), PKError> {
    let group_ref = if let Ok(uuid) = Uuid::parse_str(id_ref) {
        GroupRef::Uuid(uuid)
    } else {
        GroupRef::Hid(parse_hid(id_ref))
    };
    info!(?group_ref, ?id_ref, "resolving group id");

    let row = match group_ref {
        GroupRef::Uuid(u) => sqlx::query_as("select id, system from groups where uuid = $1")
            .bind(u)
            .fetch_optional(db)
            .await
            .map_err(|err| {
                error!(?err, ?id_ref, "failed to query group by uuid from db");
                GENERIC_SERVER_ERROR
            })?,
        GroupRef::Hid(hid) => sqlx::query_as("select id, system from groups where hid = $1")
            .bind(hid)
            .fetch_optional(db)
            .await
            .map_err(|err| {
                error!(?err, ?id_ref, "failed to query group by hid from db");
                GENERIC_SERVER_ERROR
            })?,
    };

    row.ok_or(GROUP_NOT_FOUND)
}

async fn resolve_switch_id(
    id_ref: &str,
    db: &sqlx::Pool<Postgres>,
) -> Result<(SwitchId, SystemId), PKError> {
    let uuid = Uuid::parse_str(id_ref).map_err(|_| GENERIC_BAD_REQUEST)?;
    info!(?uuid, ?id_ref, "resolving switch id");

    let row = sqlx::query_as("select id, system from switches where uuid = $1")
        .bind(uuid)
        .fetch_optional(db)
        .await
        .map_err(|err| {
            error!(?err, ?id_ref, "failed to query switch by uuid from db");
            GENERIC_SERVER_ERROR
        })?;

    row.ok_or(SWITCH_NOT_FOUND)
}

#[derive(Clone, Debug)]
pub struct RequestAboutSystem {
    pub id: SystemId,
}

#[derive(Clone, Debug)]
pub struct RequestAboutMember {
    pub id: MemberId,
    pub system: SystemId,
}

#[derive(Clone, Debug)]
pub struct RequestAboutGroup {
    pub id: GroupId,
    pub system: SystemId,
}

#[derive(Clone, Debug)]
pub struct RequestAboutSwitch {
    pub id: SwitchId,
    pub system: SystemId,
}

#[derive(Debug)]
enum SystemRef {
    Uuid(Uuid),
    DiscordUid(i64),
    Hid(String),
}

impl SystemRef {
    fn from(s: &str) -> Self {
        let uuid_opt = Uuid::parse_str(s).map(SystemRef::Uuid).ok();
        let discord_opt = s.parse::<i64>().map(SystemRef::DiscordUid).ok();
        let hid = SystemRef::Hid(parse_hid(s));
        uuid_opt.or(discord_opt).unwrap_or(hid)
    }
}

#[derive(Debug)]
enum MemberRef {
    Uuid(Uuid),
    Hid(String),
}

#[derive(Debug)]
enum GroupRef {
    Uuid(Uuid),
    Hid(String),
}
