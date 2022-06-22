use crate::config::BotConfig;
use once_cell::sync::Lazy;
use redis::aio::ConnectionManager;
use std::fmt::Debug;
use std::time::Duration;
use tracing::{error, info};
use twilight_gateway::Event;
use twilight_gateway_queue::Queue;
use twilight_model::gateway::event::{DispatchEvent, GatewayEvent, GatewayEventDeserializer};

#[derive(Clone)]
pub struct RedisEventProxy {
    // todo: don't know if i want this struct to have responsibility for ignoring calls if redis is disabled
    inner: Option<ConnectionManager>,
}

// events that should be sent in the raw handler
// does not include message create/update since we want first dibs on those and pass them on later
static ALLOWED_EVENTS: Lazy<Vec<&'static str>> = Lazy::new(|| {
    vec![
        "INTERACTION_CREATE",
        "MESSAGE_DELETE",
        "MESSAGE_DELETE_BULK",
        "MESSAGE_REACTION_ADD",
        "READY",
        "GUILD_CREATE",
        "GUILD_UPDATE",
        "GUILD_DELETE",
        "GUILD_ROLE_CREATE",
        "GUILD_ROLE_UPDATE",
        "GUILD_ROLE_DELETE",
        "CHANNEL_CREATE",
        "CHANNEL_UPDATE",
        "CHANNEL_DELETE",
        "THREAD_CREATE",
        "THREAD_UPDATE",
        "THREAD_DELETE",
        "THREAD_LIST_SYNC",
    ]
});

impl RedisEventProxy {
    async fn send_event_raw_inner(&mut self, shard_id: u64, payload: &[u8]) -> anyhow::Result<()> {
        if let Some(ref mut redis) = self.inner {
            info!(shard_id = shard_id, "publishing event");
            let key = format!("evt-{}", shard_id);

            redis::cmd("PUBLISH")
                .arg(&key[..])
                .arg(payload)
                .query_async(redis)
                .await?;
        }

        Ok(())
    }

    pub async fn send_event_raw(&mut self, shard_id: u64, payload: &[u8]) -> anyhow::Result<()> {
        let payload_str = std::str::from_utf8(payload)?;

        if let Some(deser) = GatewayEventDeserializer::from_json(payload_str) {
            if let Some(event_type) = deser.event_type_ref() {
                if ALLOWED_EVENTS.contains(&event_type) {
                    self.send_event_raw_inner(shard_id, payload).await?;
                }
            }
        }

        Ok(())
    }

    pub async fn send_event_parsed(&mut self, shard_id: u64, evt: Event) -> anyhow::Result<()> {
        info!(shard_id, "sending parsed: {:?}", evt.kind());

        let dispatch_event = DispatchEvent::try_from(evt)?;
        let gateway_event = GatewayEvent::Dispatch(0, Box::new(dispatch_event));
        let buf = serde_json::to_vec(&gateway_event)?;
        self.send_event_raw_inner(shard_id, &buf).await?;
        Ok(())
    }
}

pub async fn connect_to_redis(addr: &str) -> anyhow::Result<ConnectionManager> {
    let client = redis::Client::open(addr)?;
    info!("connecting to redis at {}...", addr);
    Ok(ConnectionManager::new(client).await?)
}

pub async fn init_event_proxy(config: &BotConfig) -> anyhow::Result<RedisEventProxy> {
    let mgr = if let Some(redis_addr) = &config.redis_addr {
        Some(connect_to_redis(redis_addr).await?)
    } else {
        info!("no redis address specified, skipping");
        None
    };

    Ok(RedisEventProxy { inner: mgr })
}

#[derive(Clone)]
pub struct RedisQueue {
    pub redis: ConnectionManager,
    pub concurrency: u64,
}

impl Debug for RedisQueue {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("RedisQueue")
            .field("concurrency", &self.concurrency)
            .finish()
    }
}

impl Queue for RedisQueue {
    fn request<'a>(
        &'a self,
        shard_id: [u64; 2],
    ) -> std::pin::Pin<Box<dyn futures::Future<Output = ()> + Send + 'a>> {
        Box::pin(request_inner(
            self.redis.clone(),
            self.concurrency,
            *shard_id.first().unwrap(),
        ))
    }
}

async fn request_inner(mut client: ConnectionManager, concurrency: u64, shard_id: u64) {
    let bucket = shard_id % concurrency;
    let key = format!("pluralkit:identify:{}", bucket);

    // SET bucket 1 EX 6 NX = write a key expiring after 6 seconds if there's not already one
    let mut cmd = redis::cmd("SET");
    cmd.arg(key).arg("1").arg("EX").arg(6i8).arg("NX");

    info!(shard_id, bucket, "waiting for allowance...");
    loop {
        let done = cmd
            .clone()
            .query_async::<_, Option<String>>(&mut client)
            .await;
        match done {
            Ok(Some(_)) => {
                info!(shard_id, bucket, "got allowance!");
                return;
            }
            Ok(None) => {
                // not allowed yet, waiting
            }
            Err(e) => {
                error!(shard_id, bucket, "error getting shard allowance: {}", e)
            }
        }

        tokio::time::sleep(Duration::from_millis(500)).await;
    }
}

pub async fn init_gateway_queue(config: &BotConfig) -> anyhow::Result<Option<RedisQueue>> {
    let queue = if let Some(ref addr) = config.redis_gateway_queue_addr {
        let redis = connect_to_redis(addr).await?;
        let concurrency = config.max_concurrency.unwrap_or(1);
        Some(RedisQueue { redis, concurrency })
    } else {
        None
    };

    Ok(queue)
}
