use deadpool_postgres::Pool;
use futures::StreamExt;
use redis::AsyncCommands;
use std::{sync::Arc, env};
use tracing::{error, info, Level};

use twilight_cache_inmemory::{InMemoryCache, ResourceType};
use twilight_gateway::{
    cluster::{Events, ShardScheme},
    Cluster, Event, EventTypeFlags, Intents,
};
use twilight_http::Client as HttpClient;

mod config;
mod db;
mod util;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    init_tracing();
    info!("starting...");

    let cfg = config::load_config();

    let http = Arc::new(HttpClient::new(cfg.token.clone()));
    let rconn = redis::Client::open(cfg.redis_addr.clone()).unwrap();
    let (_cluster, events) = init_gateway(&cfg, rconn.clone()).await?;
    let cache = init_cache();
    let db = db::init_db(&cfg).await?;

    run(http, events, cache, db, rconn).await?;

    Ok(())
}

async fn run(
    http: Arc<HttpClient>,
    mut events: Events,
    cache: Arc<InMemoryCache>,
    db: Pool,
    rconn: redis::Client,
) -> anyhow::Result<()> {
    while let Some((shard_id, event)) = events.next().await {

        cache.update(&event);

        let http_cloned = http.clone();
        let cache_cloned = cache.clone();
        let db_cloned = db.clone();
        let rconn_cloned = rconn.clone();
        
        tokio::spawn(async move {
            let result = handle_event(
                shard_id,
                event,
                http_cloned,
                cache_cloned,
                db_cloned,
                rconn_cloned
            )
            .await;
            if let Err(e) = result {
                error!("error in event handler: {:?}", e);
            }
        });
    }

    Ok(())
}

async fn handle_event<'a>(
    shard_id: u64,
    event: Event,
    http: Arc<HttpClient>,
    cache: Arc<InMemoryCache>,
    _db: Pool,
    rconn: redis::Client
) -> anyhow::Result<()> {
    match event {
        Event::GatewayInvalidateSession(resumable) => {
            info!("shard {} session invalidated, resumable? {}", shard_id, resumable);
        }
        Event::ShardConnected(_) => {
            info!("shard {} connected", shard_id);
        }
        Event::ShardDisconnected(info) => {
            info!("shard {} disconnected, code: {:?}, reason: {:?}", shard_id, info.code, info.reason);
        }
        Event::ShardPayload(payload) => {
            let mut conn = rconn.get_async_connection().await?;
            conn.publish::<&str, Vec<u8>, i32>("evt", payload.bytes).await?;
        }
        Event::MessageCreate(msg) => {
            if msg.content == "pkt;test" {
                // let message_context = db::get_message_context(
                //     &db,
                //     msg.author.id.get(),
                //     msg.guild_id.map(|x| x.get()).unwrap_or(0),
                //     msg.channel_id.get(),
                // )
                // .await?;

                // let content = format!("message context:\n```\n{:#?}\n```", message_context);
                // http.create_message(msg.channel_id)
                //     .reply(msg.id)
                //     .content(&content)?
                //     .exec()
                //     .await?;

                // let proxy_members = db::get_proxy_members(
                //     &db,
                //     msg.author.id.get(),
                //     msg.guild_id.map(|x| x.get()).unwrap_or(0),
                // )
                // .await?;

                // let content = format!("proxy members:\n```\n{:#?}\n```", proxy_members);
                // info!("{}", content);
                // http.create_message(msg.channel_id)
                //     .reply(msg.id)
                //     .content(&content)?
                //     .exec()
                //     .await?;

                let cache_stats = cache.stats();

                let pid = unsafe { libc::getpid() };
                let pagesize = {
                    unsafe {
                        libc::sysconf(libc::_SC_PAGESIZE)
                    }
                };
                
                let p = procfs::process::Process::new(pid)?;
                let content = format!(
                    "[rust]\nguilds:{}\nchannels:{}\nroles:{}\nusers:{}\nmembers:{}\n\nmemory usage: {}",
                    cache_stats.guilds(),
                    cache_stats.channels(),
                    cache_stats.roles(),
                    cache_stats.users(),
                    cache_stats.members(),
                    p.stat.rss * pagesize
                );

                http.create_message(msg.channel_id)
                .reply(msg.id)
                .content(&content)?
                .exec()
                .await?;
            }
        }
        _ => {}
    }

    Ok(())
}

fn init_tracing() {
    tracing_subscriber::fmt()
        .with_max_level(Level::INFO)
        .init();
}

async fn init_gateway(
    cfg: &config::BotConfig,
    rconn: redis::Client,
) -> anyhow::Result<(Arc<Cluster>, Events)> {
    let shard_count = cfg.shard_count.clone();

    let scheme: ShardScheme;

    if shard_count < 16 {
        scheme = ShardScheme::Auto;
    } else {
        let cluster_id = env::var("NOMAD_ALLOC_INDEX")?.parse::<u64>().unwrap();
        let first_shard_id = 16 * cluster_id;

        scheme = ShardScheme::try_from((first_shard_id..first_shard_id+16, shard_count)).unwrap();
    }

    let queue = util::RedisQueue {
        client: rconn.clone(),
        concurrency: cfg.max_concurrency.clone()
    };

    let (cluster, events) = Cluster::builder(
        cfg.token.clone(),
        Intents::GUILDS
        | Intents::DIRECT_MESSAGES
        | Intents::DIRECT_MESSAGE_REACTIONS
        | Intents::GUILD_EMOJIS_AND_STICKERS
        | Intents::GUILD_MESSAGES
        | Intents::GUILD_MESSAGE_REACTIONS
        | Intents::GUILD_WEBHOOKS
        | Intents::MESSAGE_CONTENT
    )
        .shard_scheme(scheme)
        .event_types(
            // EventTypeFlags::all()
                EventTypeFlags::READY
              | EventTypeFlags::GATEWAY_INVALIDATE_SESSION
              | EventTypeFlags::GATEWAY_RECONNECT
              | EventTypeFlags::SHARD_PAYLOAD
              | EventTypeFlags::SHARD_CONNECTED
              | EventTypeFlags::SHARD_DISCONNECTED
              | EventTypeFlags::GUILD_CREATE
              | EventTypeFlags::CHANNEL_CREATE
              | EventTypeFlags::MESSAGE_CREATE
            // | EventTypeFlags::MESSAGE_UPDATE
        )
        .queue(Arc::new(queue))
        .build()
        .await?;
    let cluster = Arc::new(cluster);

    let cluster_spawn = Arc::clone(&cluster);
    tokio::spawn(async move {
        cluster_spawn.up().await;
    });

    Ok((cluster, events))
}

fn init_cache() -> Arc<InMemoryCache> {
    let cache = InMemoryCache::builder()
        .resource_types(
              ResourceType::GUILD
            | ResourceType::CHANNEL
            | ResourceType::ROLE
            | ResourceType::USER
            // | ResourceType::MEMBER
        )
        .build();
    Arc::new(cache)
}
