use std::fmt;

use axum::{
    extract::{Request, State},
    http::{Extensions, StatusCode},
    middleware::Next,
    response::{IntoResponse, Response},
    routing::url_params::UrlParams,
};

use sqlx::Postgres;
use tracing::{error, info};

use crate::{
    ApiContext,
    error::{
        GENERIC_SERVER_ERROR, GROUP_NOT_FOUND, MEMBER_NOT_FOUND, SWITCH_NOT_FOUND, SYSTEM_NOT_FOUND,
    },
    middleware::ownership::{
        OwningSystem, RequestAboutGroup, RequestAboutMember, RequestAboutSwitch, RequestAboutSystem,
    },
    util::json_err,
};
use crate::{
    auth::AuthState,
    error::{GENERIC_BAD_REQUEST, PKError},
};
use pluralkit_models::{GroupRef, MemberRef, ResolveId, SwitchRef, SystemRef};

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
                let Some(system_id) = req
                    .extensions()
                    .get::<AuthState>()
                    .expect("missing auth state")
                    .system_id()
                else {
                    return json_err(
                        StatusCode::UNAUTHORIZED,
                        r#"{"message":"401: Missing or invalid Authorization header","code": 0}"#
                            .to_string(),
                    )
                    .into();
                };

                req.extensions_mut()
                    .insert(RequestAboutSystem { id: system_id });
            }
            "system_id" => {
                match lookup_ids_for_id_ref::<SystemRef, RequestAboutSystem>(
                    id_ref,
                    SYSTEM_NOT_FOUND,
                    &ctx.db,
                    req.extensions_mut(),
                )
                .await
                {
                    Err(fail_fast_response) => return fail_fast_response.into_response(),
                    _ => (),
                }
            }
            "member_id" => {
                match lookup_ids_for_id_ref::<MemberRef, RequestAboutMember>(
                    id_ref,
                    MEMBER_NOT_FOUND,
                    &ctx.db,
                    req.extensions_mut(),
                )
                .await
                {
                    Err(fail_fast_response) => return fail_fast_response.into_response(),
                    _ => (),
                }
            }
            "group_id" => {
                match lookup_ids_for_id_ref::<GroupRef, RequestAboutGroup>(
                    id_ref,
                    GROUP_NOT_FOUND,
                    &ctx.db,
                    req.extensions_mut(),
                )
                .await
                {
                    Err(fail_fast_response) => return fail_fast_response.into_response(),
                    _ => (),
                }
            }
            "switch_id" => {
                match lookup_ids_for_id_ref::<SwitchRef, RequestAboutSwitch>(
                    id_ref,
                    SWITCH_NOT_FOUND,
                    &ctx.db,
                    req.extensions_mut(),
                )
                .await
                {
                    Err(fail_fast_response) => return fail_fast_response.into_response(),
                    _ => (),
                }
            }
            "guild_id" => {}
            _ => {}
        }
    }

    next.run(req).await
}

async fn lookup_ids_for_id_ref<
    'a,
    IdRef: ResolveId + TryFrom<&'a str> + fmt::Debug,
    RequestAboutObject: From<<IdRef as ResolveId>::Out> + OwningSystem,
>(
    id_ref: &'a str,
    not_found_err: PKError,
    db: &sqlx::Pool<Postgres>,
    insert_into: &mut Extensions,
) -> Result<(), PKError> {
    let kind = <IdRef as ResolveId>::kind();

    info!(?kind, ?id_ref, "looking up object");

    let id_ref: IdRef = id_ref.try_into().map_err(|_| GENERIC_BAD_REQUEST)?;

    match id_ref.resolve_id(db).await {
        Ok(Some(resolved_ids)) => {
            let requested_resource_id: RequestAboutObject = resolved_ids.into();
            insert_into.insert(requested_resource_id.clone());
            insert_into.insert(RequestAboutSystem {
                id: requested_resource_id.system_id(),
            });
            Ok(())
        }
        Ok(None) => Err(not_found_err),
        Err(err) => {
            let error_str = format!("failed to query {kind} from path in db");
            error!(?err, ?id_ref, error_str);
            Err(GENERIC_SERVER_ERROR)
        }
    }
}
