use fred::{clients::RedisPool, interfaces::HashesInterface};
use metrics::{counter, gauge};
use tracing::info;
use twilight_gateway::Event;

use std::collections::HashMap;

use libpk::state::ShardState;

use super::gateway::cluster_config;

#[derive(Clone)]
pub struct ShardStateManager {
    redis: RedisPool,
    shards: HashMap<u32, ShardState>,
}

pub fn new(redis: RedisPool) -> ShardStateManager {
    ShardStateManager {
        redis: redis,
        shards: HashMap::new(),
    }
}

impl ShardStateManager {
    pub async fn handle_event(&mut self, shard_id: u32, event: Event) -> anyhow::Result<()> {
        match event {
            Event::Ready(_) => self.ready_or_resumed(shard_id, false).await,
            Event::Resumed => self.ready_or_resumed(shard_id, true).await,
            _ => Ok(()),
        }
    }

    async fn save_shard(&mut self, shard_id: u32) -> anyhow::Result<()> {
        let info = self.shards.get(&shard_id);
        self.redis
            .hset::<(), &str, (String, String)>(
                "pluralkit:shardstatus",
                (
                    shard_id.to_string(),
                    serde_json::to_string(&info).expect("could not serialize shard"),
                ),
            )
            .await?;
        Ok(())
    }

    async fn ready_or_resumed(&mut self, shard_id: u32, resumed: bool) -> anyhow::Result<()> {
        info!(
            "shard {} {}",
            shard_id,
            if resumed { "resumed" } else { "ready" }
        );
        counter!(
            "pluralkit_gateway_shard_reconnect",
            "shard_id" => shard_id.to_string(),
            "resumed" => resumed.to_string(),
        )
        .increment(1);
        gauge!("pluralkit_gateway_shard_up").increment(1);

        let info = self.shards.entry(shard_id).or_insert(ShardState::default());
        info.shard_id = shard_id as i32;
        info.cluster_id = Some(cluster_config().node_id as i32);
        info.last_connection = chrono::offset::Utc::now().timestamp() as i32;
        info.up = true;

        self.save_shard(shard_id).await?;
        Ok(())
    }

    pub async fn socket_closed(&mut self, shard_id: u32) -> anyhow::Result<()> {
        gauge!("pluralkit_gateway_shard_up").decrement(1);

        let info = self.shards.entry(shard_id).or_insert(ShardState::default());
        info.shard_id = shard_id as i32;
        info.cluster_id = Some(cluster_config().node_id as i32);
        info.up = false;
        info.disconnection_count += 1;

        self.save_shard(shard_id).await?;
        Ok(())
    }

    pub async fn heartbeated(&mut self, shard_id: u32, latency: i32) -> anyhow::Result<()> {
        gauge!("pluralkit_gateway_shard_latency", "shard_id" => shard_id.to_string()).set(latency);

        let info = self.shards.entry(shard_id).or_insert(ShardState::default());
        info.shard_id = shard_id as i32;
        info.cluster_id = Some(cluster_config().node_id as i32);
        info.up = true;
        info.last_heartbeat = chrono::offset::Utc::now().timestamp() as i32;
        info.latency = latency;

        self.save_shard(shard_id).await?;
        Ok(())
    }
}
