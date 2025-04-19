use axum::{
    extract::{ConnectInfo, Path, State},
    http::StatusCode,
    response::{IntoResponse, Response},
    routing::{delete, get, post},
    Router,
};
use libpk::runtime_config::RuntimeConfig;
use serde_json::{json, to_string};
use tracing::{error, info};
use twilight_model::id::{marker::ChannelMarker, Id};

use crate::{
    discord::{
        cache::{dm_channel, DiscordCache, DM_PERMISSIONS},
        gateway::cluster_config,
    },
    event_awaiter::{AwaitEventRequest, EventAwaiter},
};
use std::{net::SocketAddr, sync::Arc};

fn status_code(code: StatusCode, body: String) -> Response {
    (code, body).into_response()
}

// this function is manually formatted for easier legibility of route_services
#[rustfmt::skip]
pub async fn run_server(cache: Arc<DiscordCache>, runtime_config: Arc<RuntimeConfig>, awaiter: Arc<EventAwaiter>) -> anyhow::Result<()> {
    // hacky fix for `move`
    let runtime_config_for_post = runtime_config.clone();
    let runtime_config_for_delete = runtime_config.clone();
    let awaiter_for_clear = awaiter.clone();

    let app = Router::new()
        .route(
            "/guilds/:guild_id",
            get(|State(cache): State<Arc<DiscordCache>>, Path(guild_id): Path<u64>| async move {
                match cache.guild(Id::new(guild_id)) {
                    Some(guild) => status_code(StatusCode::FOUND, to_string(&guild).unwrap()),
                    None => status_code(StatusCode::NOT_FOUND, "".to_string()),
                }
            }),
        )
        .route(
            "/guilds/:guild_id/members/@me",
            get(|State(cache): State<Arc<DiscordCache>>, Path(guild_id): Path<u64>| async move {
                match cache.0.member(Id::new(guild_id), libpk::config.discord.as_ref().expect("missing discord config").client_id) {
                    Some(member) => status_code(StatusCode::FOUND, to_string(member.value()).unwrap()),
                    None => status_code(StatusCode::NOT_FOUND, "".to_string()),
                }
            }),
        )
        .route(
            "/guilds/:guild_id/permissions/@me",
            get(|State(cache): State<Arc<DiscordCache>>, Path(guild_id): Path<u64>| async move {
                match cache.guild_permissions(Id::new(guild_id), libpk::config.discord.as_ref().expect("missing discord config").client_id).await {
                    Ok(val) => {
                        status_code(StatusCode::FOUND, to_string(&val.bits()).unwrap())
                    },
                    Err(err) => {
                        error!(?err, ?guild_id, "failed to get own guild member permissions");
                        status_code(StatusCode::INTERNAL_SERVER_ERROR, "".to_string())
                    },
                }
            }),
        )
        .route(
            "/guilds/:guild_id/permissions/:user_id",
            get(|State(cache): State<Arc<DiscordCache>>, Path((guild_id, user_id)): Path<(u64, u64)>| async move {
                match cache.guild_permissions(Id::new(guild_id), Id::new(user_id)).await {
                    Ok(val) => status_code(StatusCode::FOUND, to_string(&val.bits()).unwrap()),
                    Err(err) => {
                        error!(?err, ?guild_id, ?user_id, "failed to get guild member permissions");
                        status_code(StatusCode::INTERNAL_SERVER_ERROR, "".to_string())
                    },
                }
            }),
        )

        .route(
            "/guilds/:guild_id/channels",
            get(|State(cache): State<Arc<DiscordCache>>, Path(guild_id): Path<u64>| async move {
                let channel_ids = match cache.0.guild_channels(Id::new(guild_id)) {
                    Some(channels) => channels.to_owned(),
                    None => return status_code(StatusCode::NOT_FOUND, "".to_string()),
                };

                let mut channels = Vec::new();
                for id in channel_ids {
                    match cache.0.channel(id) {
                        Some(channel) => channels.push(channel.to_owned()),
                        None => {
                            tracing::error!(
                                channel_id = id.get(),
                                "referenced channel {} from guild {} not found in cache",
                                id.get(), guild_id,
                            );
                            return status_code(StatusCode::INTERNAL_SERVER_ERROR, "".to_string());
                        }
                    }
                }

                status_code(StatusCode::FOUND, to_string(&channels).unwrap())
            })
        )
        .route(
            "/guilds/:guild_id/channels/:channel_id",
            get(|State(cache): State<Arc<DiscordCache>>, Path((guild_id, channel_id)): Path<(u64, u64)>| async move {
                if guild_id == 0 {
                    return status_code(StatusCode::FOUND, to_string(&dm_channel(Id::new(channel_id))).unwrap());
                }
                match cache.0.channel(Id::new(channel_id)) {
                    Some(channel) => status_code(StatusCode::FOUND, to_string(channel.value()).unwrap()),
                    None => status_code(StatusCode::NOT_FOUND, "".to_string())
                }
            })
        )
        .route(
            "/guilds/:guild_id/channels/:channel_id/permissions/@me",
            get(|State(cache): State<Arc<DiscordCache>>, Path((guild_id, channel_id)): Path<(u64, u64)>| async move {
                if guild_id == 0 {
                    return status_code(StatusCode::FOUND, to_string(&*DM_PERMISSIONS).unwrap());
                }
                match cache.channel_permissions(Id::new(channel_id), libpk::config.discord.as_ref().expect("missing discord config").client_id).await {
                    Ok(val) => status_code(StatusCode::FOUND, to_string(&val).unwrap()),
                    Err(err) => {
                        error!(?err, ?channel_id, ?guild_id, "failed to get own channelpermissions");
                        status_code(StatusCode::INTERNAL_SERVER_ERROR, "".to_string())
                    },
                }
            }),
        )
        .route(
            "/guilds/:guild_id/channels/:channel_id/permissions/:user_id",
            get(|| async { "todo" }),
        )
        .route(
            "/guilds/:guild_id/channels/:channel_id/last_message",
            get(|State(cache): State<Arc<DiscordCache>>, Path((_guild_id, channel_id)): Path<(u64, Id<ChannelMarker>)>| async move {
                let lm = cache.get_last_message(channel_id).await;
                status_code(StatusCode::FOUND, to_string(&lm).unwrap())
            }),
        )

        .route(
            "/guilds/:guild_id/roles",
            get(|State(cache): State<Arc<DiscordCache>>, Path(guild_id): Path<u64>| async move {
                let role_ids = match cache.0.guild_roles(Id::new(guild_id)) {
                    Some(roles) => roles.to_owned(),
                    None => return status_code(StatusCode::NOT_FOUND, "".to_string()),
                };

                let mut roles = Vec::new();
                for id in role_ids {
                    match cache.0.role(id) {
                        Some(role) => roles.push(role.value().resource().to_owned()),
                        None => {
                            tracing::error!(
                                role_id = id.get(),
                                "referenced role {} from guild {} not found in cache",
                                id.get(), guild_id,
                            );
                            return status_code(StatusCode::INTERNAL_SERVER_ERROR, "".to_string());
                        }
                    }
                }

                status_code(StatusCode::FOUND, to_string(&roles).unwrap())
            })
        )

        .route("/stats", get(|State(cache): State<Arc<DiscordCache>>| async move {
            let cluster = cluster_config();
            let has_been_up = cache.2.read().await.len() as u32 == if cluster.total_shards > 16 {16} else {cluster.total_shards};
            let stats = cache.0.stats();
            let stats = json!({
                "guild_count": stats.guilds(),
                "channel_count": stats.channels(),
                // just put this here until prom stats
                "unavailable_guild_count": stats.unavailable_guilds(),
                "up": has_been_up,
            });
            status_code(StatusCode::FOUND, to_string(&stats).unwrap())
        }))

        .route("/runtime_config", get(|| async move {
            status_code(StatusCode::FOUND, to_string(&runtime_config.get_all().await).unwrap())
        }))
        .route("/runtime_config/:key", post(|Path(key): Path<String>, body: String| async move {
            let runtime_config = runtime_config_for_post;
            runtime_config.set(key, body).await.expect("failed to update runtime config");
            status_code(StatusCode::FOUND, to_string(&runtime_config.get_all().await).unwrap())
        }))
        .route("/runtime_config/:key", delete(|Path(key): Path<String>| async move {
            let runtime_config = runtime_config_for_delete;
            runtime_config.delete(key).await.expect("failed to update runtime config");
            status_code(StatusCode::FOUND, to_string(&runtime_config.get_all().await).unwrap())
        }))

        .route("/await_event", post(|ConnectInfo(addr): ConnectInfo<SocketAddr>, body: String| async move {
            info!("got request: {body} from: {addr}");
            let Ok(req) = serde_json::from_str::<AwaitEventRequest>(&body) else {
                return status_code(StatusCode::BAD_REQUEST, "".to_string());
            };

            awaiter.handle_request(req, addr).await;
            status_code(StatusCode::NO_CONTENT, "".to_string())
        }))
        .route("/clear_awaiter", post(|| async move {
            awaiter_for_clear.clear().await;
            status_code(StatusCode::NO_CONTENT, "".to_string())
        }))

        .layer(axum::middleware::from_fn(crate::logger::logger))
        .with_state(cache);

    let addr: &str = libpk::config.discord.as_ref().expect("missing discord config").cache_api_addr.as_ref();
    let listener = tokio::net::TcpListener::bind(addr).await?;
    info!("listening on {}", addr);
    axum::serve(listener, app.into_make_service_with_connect_info::<SocketAddr>()).await?;

    Ok(())
}
