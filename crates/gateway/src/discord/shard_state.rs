use fred::{clients::RedisPool, interfaces::HashesInterface};
use metrics::{counter, gauge};
use tracing::info;
use twilight_gateway::{Event, Latency};

use libpk::state::ShardState;

#[derive(Clone)]
pub struct ShardStateManager {
    redis: RedisPool,
}

pub fn new(redis: RedisPool) -> ShardStateManager {
    ShardStateManager { redis }
}

impl ShardStateManager {
    pub async fn handle_event(&self, shard_id: u32, event: Event) -> anyhow::Result<()> {
        match event {
            Event::Ready(_) => self.ready_or_resumed(shard_id, false).await,
            Event::Resumed => self.ready_or_resumed(shard_id, true).await,
            _ => Ok(()),
        }
    }

    async fn get_shard(&self, shard_id: u32) -> anyhow::Result<ShardState> {
        let data: Option<String> = self.redis.hget("pluralkit:shardstatus", shard_id).await?;
        match data {
            Some(buf) => Ok(serde_json::from_str(&buf).expect("could not decode shard data!")),
            None => Ok(ShardState::default()),
        }
    }

    async fn save_shard(&self, shard_id: u32, info: ShardState) -> anyhow::Result<()> {
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
        let mut info = self.get_shard(shard_id).await?;
        info.last_connection = chrono::offset::Utc::now().timestamp() as i32;
        info.up = true;
        self.save_shard(shard_id, info).await?;
        Ok(())
    }

    pub async fn socket_closed(&self, shard_id: u32) -> anyhow::Result<()> {
        gauge!("pluralkit_gateway_shard_up").decrement(1);
        let mut info = self.get_shard(shard_id).await?;
        info.up = false;
        info.disconnection_count += 1;
        self.save_shard(shard_id, info).await?;
        Ok(())
    }

    pub async fn heartbeated(&self, shard_id: u32, latency: &Latency) -> anyhow::Result<()> {
        let mut info = self.get_shard(shard_id).await?;
        info.up = true;
        info.last_heartbeat = chrono::offset::Utc::now().timestamp() as i32;
        info.latency = latency
            .recent()
            .first()
            .map_or_else(|| 0, |d| d.as_millis()) as i32;
        gauge!("pluralkit_gateway_shard_latency", "shard_id" => shard_id.to_string())
            .set(info.latency);
        self.save_shard(shard_id, info).await?;
        Ok(())
    }
}
