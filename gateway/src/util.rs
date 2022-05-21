use std::time::Duration;

use twilight_gateway_queue::Queue;

#[derive(Debug, Clone)]
pub struct RedisQueue {
    pub client: redis::Client,
    pub concurrency: u64
}

impl Queue for RedisQueue {
    fn request<'a>(&'a self, shard_id: [u64; 2]) -> std::pin::Pin<Box<dyn futures::Future<Output = ()> + Send + 'a>> {
        Box::pin(request_inner(self.client.clone(), self.concurrency, *shard_id.first().unwrap()))
    }
}

async fn request_inner(client: redis::Client, concurrency: u64, shard_id: u64) {
    let mut conn = client.get_async_connection().await.unwrap();
    let key = format!("pluralkit:identify:{}", (shard_id % concurrency));

    let mut cmd = redis::cmd("SET");
    cmd.arg(key).arg("1").arg("EX").arg(6i8).arg("NX");

    loop {
        let done = cmd.clone().query_async::<redis::aio::Connection, Option<String>>(&mut conn).await;
        if done.unwrap().is_some() {
            return
        }
        tokio::time::sleep(Duration::from_millis(500)).await;
    }
}

