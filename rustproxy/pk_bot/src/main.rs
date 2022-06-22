#[cfg(not(target_env = "msvc"))]
use tikv_jemallocator::Jemalloc;

#[cfg(not(target_env = "msvc"))]
#[global_allocator]
static GLOBAL: Jemalloc = Jemalloc;

use crate::cache::DiscordCache;
use crate::redis::RedisEventProxy;
use futures::StreamExt;
use sqlx::PgPool;
use std::sync::Arc;
use tracing::{error, info};
use twilight_gateway::Event;
use twilight_http::Client as HttpClient;

mod cache;
mod config;
mod db;
mod gateway;
mod model;
mod proxy;
mod redis;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    tracing_subscriber::fmt().init();

    let config = config::load_config()?;
    info!("loaded config: {:?}", config);

    let pool = db::init_db(&config).await?;
    let http = Arc::new(HttpClient::new(config.token.clone()));
    let (_cluster, mut events) = gateway::init_gateway(Arc::clone(&http), &config).await?;
    let cache = Arc::new(DiscordCache::new());
    let redis = redis::init_event_proxy(&config).await?;

    while let Some((shard_id, event)) = events.next().await {
        let http = Arc::clone(&http);
        let pool = pool.clone();
        let cache = Arc::clone(&cache);
        let redis = redis.clone();

        tokio::spawn(async move {
            cache.handle_event(&event);

            let res = handle_event(shard_id, event, http, pool, cache, redis).await;
            if let Err(e) = res {
                error!("error handling event: {:?}", e);
            }
        });
    }

    Ok(())
}

async fn handle_event(
    shard_id: u64,
    event: Event,
    http: Arc<HttpClient>,
    pool: PgPool,
    cache: Arc<DiscordCache>,
    mut redis: RedisEventProxy,
) -> anyhow::Result<()> {
    match event {
        Event::MessageCreate(msg) => {
            if msg.content.starts_with("pk;") || msg.content.starts_with("pk!") {
                redis
                    .send_event_parsed(shard_id, Event::MessageCreate(msg))
                    .await?;
                return Ok(());
            }

            let channel_type = cache.channel_type(msg.channel_id)?;

            let ctx = db::get_message_context(
                &pool,
                msg.author.id.get() as i64,
                msg.guild_id.map(|x| x.get()).unwrap_or_default() as i64,
                msg.channel_id.get() as i64,
            )
            .await?;

            let _member_permissions =
                cache.member_permissions(msg.channel_id, msg.author.id, msg.member.as_ref())?;
            let bot_permissions = cache.bot_permissions(msg.channel_id)?;

            match proxy::check_preconditions(&msg, channel_type, bot_permissions, &ctx) {
                Ok(_) => {
                    info!("attempting to proxy");
                    proxy::do_proxy(&http, &pool, &msg, &ctx).await?;
                }
                Err(reason) => {
                    info!("skipping proxy because: {}", reason)
                }
            }
        }
        Event::ShardConnected(_) => {
            info!("connected on shard {}", shard_id);
        }
        Event::ShardPayload(payload) => {
            redis.send_event_raw(shard_id, &payload.bytes).await?;
        }
        // Other events here...
        _ => {}
    }

    Ok(())
}
