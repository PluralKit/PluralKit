use bytes::Bytes;
use fred::{clients::RedisPool, interfaces::HashesInterface};
use prost::Message;
use tracing::info;
use twilight_gateway::Event;

use libpk::{proto::*, util::redis::*};

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
            Event::GatewayClose(_) => self.socket_closed(shard_id).await,
            Event::GatewayHeartbeat(_) => self.heartbeated(shard_id).await,
            _ => Ok(()),
        }
    }

    async fn get_shard(&self, shard_id: u32) -> anyhow::Result<ShardState> {
        let data: Option<Vec<u8>> = self
            .redis
            .hget("pluralkit:shardstatus", shard_id)
            .await
            .to_option_or_error()?;
        match data {
            Some(buf) => {
                Ok(ShardState::decode(buf.as_slice()).expect("could not decode shard data!"))
            }
            None => Ok(ShardState::default()),
        }
    }

    async fn save_shard(&self, shard_id: u32, info: ShardState) -> anyhow::Result<()> {
        self.redis
            .hset(
                "pluralkit:shardstatus",
                (
                    shard_id.to_string(),
                    Bytes::copy_from_slice(&info.encode_to_vec()),
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
        let mut info = self.get_shard(shard_id).await?;
        info.last_connection = chrono::offset::Utc::now().timestamp() as i32;
        info.up = true;
        self.save_shard(shard_id, info).await?;
        Ok(())
    }

    async fn socket_closed(&self, shard_id: u32) -> anyhow::Result<()> {
        info!("shard {} closed", shard_id);
        let mut info = self.get_shard(shard_id).await?;
        info.up = false;
        info.disconnection_count += 1;
        self.save_shard(shard_id, info).await?;
        Ok(())
    }

    async fn heartbeated(&self, shard_id: u32) -> anyhow::Result<()> {
        let mut info = self.get_shard(shard_id).await?;
        info.up = true;
        info.last_heartbeat = chrono::offset::Utc::now().timestamp() as i32;
        // todo
        // info.latency = latency.recent().front().map_or_else(|| 0, |d| d.as_millis()) as i32;
        info.latency = 1;
        self.save_shard(shard_id, info).await?;
        Ok(())
    }
}
