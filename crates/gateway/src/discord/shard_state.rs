use fred::{clients::RedisPool, interfaces::HashesInterface};
use metrics::{counter, gauge};
use tokio::sync::RwLock;
use tracing::info;
use twilight_gateway::Event;

use std::collections::HashMap;

use libpk::state::ShardState;

use super::gateway::cluster_config;

pub struct ShardStateManager {
    redis: RedisPool,
    shards: RwLock<HashMap<u32, ShardState>>,
}

pub fn new(redis: RedisPool) -> ShardStateManager {
    ShardStateManager {
        redis: redis,
        shards: RwLock::new(HashMap::new()),
    }
}

impl ShardStateManager {
    pub async fn handle_event(&self, shard_id: u32, event: Event) -> anyhow::Result<()> {
        match event {
            // also update gateway.rs with event types
            Event::Ready(_) => self.ready_or_resumed(shard_id, false).await,
            Event::Resumed => self.ready_or_resumed(shard_id, true).await,
            _ => Ok(()),
        }
    }

    async fn save_shard(&self, id: u32, state: ShardState) -> anyhow::Result<()> {
        {
            let mut shards = self.shards.write().await;
            shards.insert(id, state.clone());
        }
        self.redis
            .hset::<(), &str, (String, String)>(
                "pluralkit:shardstatus",
                (
                    id.to_string(),
                    serde_json::to_string(&state).expect("could not serialize shard"),
                ),
            )
            .await?;
        Ok(())
    }

    async fn get_shard(&self, id: u32) -> Option<ShardState> {
        let shards = self.shards.read().await;
        shards.get(&id).cloned()
    }

    pub async fn get(&self) -> Vec<ShardState> {
        self.shards.read().await.values().cloned().collect()
    }

    async fn ready_or_resumed(&self, shard_id: u32, resumed: bool) -> anyhow::Result<()> {
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

        let mut info = self
            .get_shard(shard_id)
            .await
            .unwrap_or(ShardState::default());

        info.shard_id = shard_id as i32;
        info.cluster_id = Some(cluster_config().node_id as i32);
        info.last_connection = chrono::offset::Utc::now().timestamp() as i32;
        info.up = true;

        self.save_shard(shard_id, info).await?;
        Ok(())
    }

    pub async fn socket_closed(&self, shard_id: u32, reconnect: bool) -> anyhow::Result<()> {
        gauge!("pluralkit_gateway_shard_up").decrement(1);

        let mut info = self
            .get_shard(shard_id)
            .await
            .unwrap_or(ShardState::default());

        info.shard_id = shard_id as i32;
        info.cluster_id = Some(cluster_config().node_id as i32);
        info.up = false;
        info.last_reconnect = chrono::offset::Utc::now().timestamp() as i32;
        info.disconnection_count += 1;

        self.save_shard(shard_id, info).await?;
        Ok(())
    }

    pub async fn heartbeated(&self, shard_id: u32, latency: i32) -> anyhow::Result<()> {
        gauge!("pluralkit_gateway_shard_latency", "shard_id" => shard_id.to_string()).set(latency);

        let mut info = self
            .get_shard(shard_id)
            .await
            .unwrap_or(ShardState::default());

        info.shard_id = shard_id as i32;
        info.cluster_id = Some(cluster_config().node_id as i32);
        info.up = true;
        info.last_heartbeat = chrono::offset::Utc::now().timestamp() as i32;
        info.latency = latency;

        self.save_shard(shard_id, info).await?;
        Ok(())
    }
}
