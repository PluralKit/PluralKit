use axum::{
    extract::{Path, State},
    http::StatusCode,
    response::{IntoResponse, Response},
    routing::get,
    Router,
};
use serde_json::to_string;
use tracing::info;
use twilight_model::id::Id;

use crate::discord::cache::DiscordCache;
use std::sync::Arc;

fn status_code(code: StatusCode, body: String) -> Response {
    (code, body).into_response()
}

// this function is manually formatted for easier legibility of route_services
#[rustfmt::skip]
pub async fn run_server(cache: Arc<DiscordCache>) -> anyhow::Result<()> {
    let app = Router::new()
        .route(
            "/guilds/:guild_id",
            get(|State(cache): State<Arc<DiscordCache>>, Path(guild_id): Path<u64>| async move {
                match cache.0.guild(Id::new(guild_id)) {
                    Some(guild) => status_code(StatusCode::FOUND, to_string(guild.value()).unwrap()),
                    None => status_code(StatusCode::NOT_FOUND, "".to_string()),
                }
            }),
        )
        .route(
            "/guilds/:guild_id/self_member",
            get(|State(cache): State<Arc<DiscordCache>>, Path(guild_id): Path<u64>| async move {
                match cache.0.member(Id::new(guild_id), libpk::config.discord.client_id) {
                    Some(member) => status_code(StatusCode::FOUND, to_string(member.value()).unwrap()),
                    None => status_code(StatusCode::NOT_FOUND, "".to_string()),
                }
            }),
        )
        .route(
            "/guilds/:guild_id/permissions/@me",
            get(|| async { "todo" }),
        )
        .route(
            "/guilds/:guild_id/permissions/:user_id",
            get(|| async { "todo" }),
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
                        }
                    }
                }

                status_code(StatusCode::FOUND, to_string(&channels).unwrap())
            })
        )
        .route(
            "/channels/:channel_id",
            get(|State(cache): State<Arc<DiscordCache>>, Path(channel_id): Path<u64>| async move {
                match cache.0.channel(Id::new(channel_id)) {
                    Some(channel) => status_code(StatusCode::FOUND, to_string(channel.value()).unwrap()),
                    None => status_code(StatusCode::NOT_FOUND, "".to_string())
                }
            })
        )
        .route(
            "/channels/:channel_id/permissions/@me",
            get(|| async { "todo" }),
        )
        .route(
            "/channels/:channel_id/permissions/:user_id",
            get(|| async { "todo" }),
        )
        .route(
            "/channels/:channel_id/last_message",
            get(|| async { status_code(StatusCode::NOT_IMPLEMENTED, "".to_string()) }),
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
                        }
                    }
                }

                status_code(StatusCode::FOUND, to_string(&roles).unwrap())
            })
        )
        .route(
            "/roles/:role_id",
            get(|State(cache): State<Arc<DiscordCache>>, Path(role_id): Path<u64>| async move {
                match cache.0.role(Id::new(role_id)) {
                    Some(role) => status_code(StatusCode::FOUND, to_string(role.value().resource()).unwrap()),
                    None => status_code(StatusCode::NOT_FOUND, "".to_string()),
                }
            })
        )

        .with_state(cache);

    let addr: &str = libpk::config.api.addr.as_ref();
    let listener = tokio::net::TcpListener::bind(addr).await?;
    info!("listening on {}", addr);
    axum::serve(listener, app).await?;

    Ok(())
}
