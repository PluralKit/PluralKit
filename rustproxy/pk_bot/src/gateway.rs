use crate::config::BotConfig;
use crate::redis;
use std::{env, sync::Arc};
use tracing::info;
use twilight_gateway::{
    cluster::{Events, ShardScheme},
    Cluster, EventTypeFlags, Intents,
};
use twilight_http::Client;

pub async fn init_gateway(
    http: Arc<Client>,
    config: &BotConfig,
) -> anyhow::Result<(Arc<Cluster>, Events)> {
    let mut builder = Cluster::builder(
        config.token.clone(),
        Intents::GUILDS
            | Intents::DIRECT_MESSAGES
            | Intents::GUILD_MESSAGES
            | Intents::MESSAGE_CONTENT,
    );
    builder = builder.http_client(http);
    builder = builder.event_types(EventTypeFlags::all());

    if let Some(scheme) = get_shard_scheme(config)? {
        info!("using shard scheme: {:?}", scheme);
        builder = builder.shard_scheme(scheme);
    }

    if let Some(queue) = redis::init_gateway_queue(config).await? {
        info!("using redis gateway queue");
        builder = builder.queue(Arc::new(queue));
    }

    let (cluster, events) = builder.build().await?;
    let cluster = Arc::new(cluster);
    let cluster_spawn = Arc::clone(&cluster);
    tokio::spawn(async move {
        info!("starting shards...");
        cluster_spawn.up().await;
    });

    Ok((cluster, events))
}

fn get_cluster_id() -> anyhow::Result<u64> {
    Ok(env::var("NOMAD_ALLOC_INDEX")
        .unwrap_or_else(|_| "0".to_string())
        .parse::<u64>()?)
}

fn get_shard_scheme(config: &BotConfig) -> anyhow::Result<Option<ShardScheme>> {
    let shard_count = config.shard_count.unwrap_or(1);
    let scheme = if shard_count >= 16 {
        let cluster_id = get_cluster_id()?;
        let first_shard_id = 16 * cluster_id;
        let shard_range = first_shard_id..first_shard_id + 16;
        Some(ShardScheme::try_from((shard_range, shard_count))?)
    } else {
        None
    };
    Ok(scheme)
}
