use fred::{
    clients::RedisPool,
    error::RedisError,
    interfaces::KeysInterface,
    types::{Expiration, SetOptions},
};
use std::fmt::Debug;
use std::time::Duration;
use tokio::sync::oneshot;
use tracing::{error, info};
use twilight_gateway::queue::Queue;

pub fn new(redis: RedisPool) -> RedisQueue {
    RedisQueue {
        redis,
        concurrency: libpk::config
            .discord
            .as_ref()
            .expect("missing discord config")
            .max_concurrency,
    }
}

#[derive(Clone)]
pub struct RedisQueue {
    pub redis: RedisPool,
    pub concurrency: u32,
}

impl Debug for RedisQueue {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("RedisQueue")
            .field("concurrency", &self.concurrency)
            .finish()
    }
}

impl Queue for RedisQueue {
    fn enqueue<'a>(&'a self, shard_id: u32) -> oneshot::Receiver<()> {
        let (tx, rx) = oneshot::channel();

        tokio::spawn(request_inner(
            self.redis.clone(),
            self.concurrency,
            shard_id,
            tx,
        ));

        rx
    }
}

const EXPIRY: i64 = 6;
const RETRY_INTERVAL: u64 = 500;

async fn request_inner(redis: RedisPool, concurrency: u32, shard_id: u32, tx: oneshot::Sender<()>) {
    let bucket = shard_id % concurrency;
    let key = format!("pluralkit:identify:{}", bucket);

    info!(shard_id, bucket, "waiting for allowance...");
    loop {
        let done: Result<Option<String>, RedisError> = redis
            .set(
                key.to_string(),
                "1",
                Some(Expiration::EX(EXPIRY)),
                Some(SetOptions::NX),
                false,
            )
            .await;
        match done {
            Ok(Some(_)) => {
                info!(shard_id, bucket, "got allowance!");
                // if this fails, it's probably already doing something else
                let _ = tx.send(());
                return;
            }
            Ok(None) => {
                // not allowed yet, waiting
            }
            Err(e) => {
                error!(shard_id, bucket, "error getting shard allowance: {}", e)
            }
        }

        tokio::time::sleep(Duration::from_millis(RETRY_INTERVAL)).await;
    }
}
