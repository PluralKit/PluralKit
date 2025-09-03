use axum::{
    Extension, Json,
    extract::{Json as ExtractJson, State},
    response::IntoResponse,
};
use pk_macros::api_endpoint;
use sea_query::{Expr, ExprTrait, PostgresQueryBuilder};
use sea_query_sqlx::SqlxBinder;
use serde_json::{Value, json};

use pluralkit_models::{PKGroup, PKGroupPatch, PKMember, PKMemberPatch, PKSystem};

use crate::{
    ApiContext,
    auth::AuthState,
    error::{
        GENERIC_AUTH_ERROR, NOT_OWN_GROUP, NOT_OWN_MEMBER, PKError, TARGET_GROUP_NOT_FOUND,
        TARGET_MEMBER_NOT_FOUND,
    },
};

#[derive(serde::Deserialize, Debug)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum BulkActionRequestFilter {
    All,
    Ids { ids: Vec<String> },
    Connection { id: String },
}

#[derive(serde::Deserialize, Debug)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum BulkActionRequest {
    Member {
        filter: BulkActionRequestFilter,
        patch: PKMemberPatch,
    },
    Group {
        filter: BulkActionRequestFilter,
        patch: PKGroupPatch,
    },
}

#[api_endpoint]
pub async fn bulk(
    Extension(auth): Extension<AuthState>,
    State(ctx): State<ApiContext>,
    ExtractJson(req): ExtractJson<BulkActionRequest>,
) -> Json<Value> {
    let Some(system_id) = auth.system_id() else {
        return Err(GENERIC_AUTH_ERROR);
    };

    #[derive(sqlx::FromRow)]
    struct Ider {
        id: i32,
        hid: String,
        uuid: String,
    }

    #[derive(sqlx::FromRow)]
    struct GroupMemberEntry {
        member_id: i32,
        group_id: i32,
    }

    #[allow(dead_code)]
    #[derive(sqlx::FromRow)]
    struct OnlyIder {
        id: i32,
    }

    println!("BulkActionRequest::{req:#?}");
    match req {
        BulkActionRequest::Member { filter, mut patch } => {
            patch.validate_bulk();
            if patch.errors().len() > 0 {
                return Err(PKError::from_validation_errors(patch.errors()));
            }

            let ids: Vec<i32> = match filter {
                BulkActionRequestFilter::All => {
                    let ids: Vec<Ider> = sqlx::query_as("select id from members where system = $1")
                        .bind(system_id as i64)
                        .fetch_all(&ctx.db)
                        .await?;
                    ids.iter().map(|v| v.id).collect()
                }
                BulkActionRequestFilter::Ids { ids } => {
                    let members: Vec<PKMember> = sqlx::query_as(
                        "select * from members where hid = any($1::array) or uuid::text = any($1::array)",
                    )
                    .bind(&ids)
                    .fetch_all(&ctx.db)
                    .await?;

                    // todo: better errors
                    if members.len() != ids.len() {
                        return Err(TARGET_MEMBER_NOT_FOUND);
                    }

                    if members.iter().any(|m| m.system != system_id) {
                        return Err(NOT_OWN_MEMBER);
                    }

                    members.iter().map(|m| m.id).collect()
                }
                BulkActionRequestFilter::Connection { id } => {
                    let Some(group): Option<PKGroup> =
                        sqlx::query_as("select * from groups where hid = $1 or uuid::text = $1")
                            .bind(id)
                            .fetch_optional(&ctx.db)
                            .await?
                    else {
                        return Err(TARGET_GROUP_NOT_FOUND);
                    };

                    if group.system != system_id {
                        return Err(NOT_OWN_GROUP);
                    }

                    let entries: Vec<GroupMemberEntry> =
                        sqlx::query_as("select * from group_members where group_id = $1")
                            .bind(group.id)
                            .fetch_all(&ctx.db)
                            .await?;

                    entries.iter().map(|v| v.member_id).collect()
                }
            };

            let (q, pms) = patch
                .to_sql()
                .table("members") // todo: this should be in the model definition
                .and_where(Expr::col("id").is_in(ids))
                .returning_col("id")
                .build_sqlx(PostgresQueryBuilder);

            let res: Vec<OnlyIder> = sqlx::query_as_with(&q, pms).fetch_all(&ctx.db).await?;
            Ok(Json(json! {{ "updated": res.len() }}))
        }
        BulkActionRequest::Group { filter, mut patch } => {
            patch.validate_bulk();
            if patch.errors().len() > 0 {
                return Err(PKError::from_validation_errors(patch.errors()));
            }

            let ids: Vec<i32> = match filter {
                BulkActionRequestFilter::All => {
                    let ids: Vec<Ider> = sqlx::query_as("select id from groups where system = $1")
                        .bind(system_id as i64)
                        .fetch_all(&ctx.db)
                        .await?;
                    ids.iter().map(|v| v.id).collect()
                }
                BulkActionRequestFilter::Ids { ids } => {
                    let groups: Vec<PKGroup> = sqlx::query_as(
                        "select * from groups where hid = any($1) or uuid::text = any($1)",
                    )
                    .bind(&ids)
                    .fetch_all(&ctx.db)
                    .await?;

                    // todo: better errors
                    if groups.len() != ids.len() {
                        return Err(TARGET_GROUP_NOT_FOUND);
                    }

                    if groups.iter().any(|m| m.system != system_id) {
                        return Err(NOT_OWN_GROUP);
                    }

                    groups.iter().map(|m| m.id).collect()
                }
                BulkActionRequestFilter::Connection { id } => {
                    let Some(member): Option<PKMember> =
                        sqlx::query_as("select * from members where hid = $1 or uuid::text = $1")
                            .bind(id)
                            .fetch_optional(&ctx.db)
                            .await?
                    else {
                        return Err(TARGET_MEMBER_NOT_FOUND);
                    };

                    if member.system != system_id {
                        return Err(NOT_OWN_MEMBER);
                    }

                    let entries: Vec<GroupMemberEntry> =
                        sqlx::query_as("select * from group_members where member_id = $1")
                            .bind(member.id)
                            .fetch_all(&ctx.db)
                            .await?;

                    entries.iter().map(|v| v.group_id).collect()
                }
            };

            let (q, pms) = patch
                .to_sql()
                .table("groups") // todo: this should be in the model definition
                .and_where(Expr::col("id").is_in(ids))
                .returning_col("id")
                .build_sqlx(PostgresQueryBuilder);

            println!("{q:#?} {pms:#?}");

            let res: Vec<OnlyIder> = sqlx::query_as_with(&q, pms).fetch_all(&ctx.db).await?;
            Ok(Json(json! {{ "updated": res.len() }}))
        }
    }
}
