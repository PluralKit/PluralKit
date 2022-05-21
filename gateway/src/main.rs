use deadpool_postgres::Pool;
use futures::StreamExt;
use std::{sync::Arc, env};
use tracing::{error, info, Level};

use twilight_gateway::{
    cluster::{Events, ShardScheme},
    Cluster, EventTypeFlags, Intents,
};
use twilight_http::Client as HttpClient;

mod config;
mod evt;
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
    // let cache = init_cache();
    let db = db::init_db(&cfg).await?;

    run(http, events, db, rconn).await?;

    Ok(())
}

async fn run(
    http: Arc<HttpClient>,
    mut events: Events,
    db: Pool,
    rconn: redis::Client,
) -> anyhow::Result<()> {
    while let Some((shard_id, event)) = events.next().await {

        // cache.update(&event);

        let http_cloned = http.clone();
        let db_cloned = db.clone();
        let rconn_cloned = rconn.clone();
        
        tokio::spawn(async move {
            let result = evt::handle_event(
                shard_id,
                event,
                http_cloned,
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
        let cluster_id = env::var("NOMAD_ALLOC_INDEX").or::<String>(Result::Ok("0".to_string())).unwrap().parse::<u64>().unwrap();
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
        .event_types(EventTypeFlags::all())
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
