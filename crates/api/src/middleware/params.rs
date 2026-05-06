use axum::{
    extract::{Request, State},
    http::StatusCode,
    middleware::Next,
    response::{IntoResponse, Response},
    routing::url_params::UrlParams,
};
use sqlx::types::Uuid;
use tracing::warn;

use crate::{
    auth::{AuthState, Authable}, error::{self, PKError}, util::json_err, ApiContext
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
        let id_ref = parse_hid(value.as_str());
        let id_ref = id_ref.as_str();
        let request_about = match key.as_ref() {
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

                Ok(Some(RequestAbout::System(system_id)))
            }
            "system_id" if Uuid::parse_str(id_ref).is_ok() => {
                resolve_entity(&ctx.db, "systems", "uuid", id_ref).await
            }
            "system_id" if let Ok(discord_id) = id_ref.parse::<i64>() => {
                sqlx::query_as::<_, ResolveEntityRow>(
                    "select 0 as id, system from accounts where uid = $1",
                )
                .bind(discord_id)
                .fetch_optional(&ctx.db)
                .await
                .map_err(PKError::from)
                .and_then(|v| v.ok_or(error::SYSTEM_NOT_FOUND))
                .map(|v| Some(RequestAbout::System(v.system)))
            }
            "system_id" => resolve_entity(&ctx.db, "systems", "hid", id_ref).await,
            "member_id" if Uuid::parse_str(id_ref).is_ok() => {
                resolve_entity(&ctx.db, "members", "uuid", id_ref).await
            }
            "member_id" => resolve_entity(&ctx.db, "members", "hid", id_ref).await,
            "group_id" if Uuid::parse_str(id_ref).is_ok() => {
                resolve_entity(&ctx.db, "groups", "uuid", id_ref).await
            }
            "group_id" => resolve_entity(&ctx.db, "groups", "hid", id_ref).await,
            "switch_id" => resolve_entity(&ctx.db, "switches", "uuid", id_ref).await,
            _ => {
                warn!("unmatched request param {key}");
                Ok(None)
            }
        };

        match request_about {
            Ok(Some(about)) => {
                req.extensions_mut().insert(about);
            }
            Err(err) => return err.into_response(),
            Ok(None) => {}
        }
    }

    next.run(req).await
}

#[allow(dead_code)]
#[derive(Clone)]
pub enum RequestAbout {
    System(SystemId),
    Member { id: MemberId, system: SystemId },
    Group { id: GroupId, system: SystemId },
    Switch { id: SwitchId, system: SystemId },
}

impl RequestAbout {
    pub fn system_id(&self) -> SystemId {
        match self {
            Self::System(id) => *id,
            Self::Member { system, .. } => *system,
            Self::Group { system, .. } => *system,
            Self::Switch { system, .. } => *system,
        }
    }
}

impl Authable for RequestAbout {
    fn authable_system_id(&self) -> SystemId {
        self.system_id()
    }
}

#[derive(sqlx::FromRow)]
struct ResolveEntityRow {
    id: i32,
    system: i32,
}

async fn resolve_entity(
    pool: &sqlx::postgres::PgPool,
    table: &str,
    column: &str,
    value: &str,
) -> Result<Option<RequestAbout>, PKError> {
    let system_col = if table == "systems" {
        "0 as system"
    } else {
        "system"
    };
    let maybe_uuid = if column == "uuid" { "$1::uuid" } else { "$1" };

    let Some(row): Option<ResolveEntityRow> = sqlx::query_as(
        format!("select id, {system_col} from {table} where {column} = {maybe_uuid}").as_str(),
    )
    .bind(value)
    .fetch_optional(pool)
    .await
    .map_err(PKError::from)?
    else {
        return Err(match table {
            "systems" => error::SYSTEM_NOT_FOUND,
            "members" => error::MEMBER_NOT_FOUND,
            "groups" => error::GROUP_NOT_FOUND,
            "switches" => error::SWITCH_NOT_FOUND,
            _ => unreachable!(),
        });
    };

    match table {
        "systems" => Ok(Some(RequestAbout::System(row.id))),
        "members" => Ok(Some(RequestAbout::Member {
            id: row.id,
            system: row.system,
        })),
        "groups" => Ok(Some(RequestAbout::Group {
            id: row.id,
            system: row.system,
        })),
        "switches" => Ok(Some(RequestAbout::Switch {
            id: row.id,
            system: row.system,
        })),
        _ => unreachable!(),
    }
}
