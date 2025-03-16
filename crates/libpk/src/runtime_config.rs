use fred::{clients::RedisPool, interfaces::HashesInterface};
use std::collections::HashMap;
use tokio::sync::RwLock;
use tracing::info;

pub struct RuntimeConfig {
    redis: RedisPool,
    settings: RwLock<HashMap<String, String>>,
    redis_key: String,
}

impl RuntimeConfig {
    pub async fn new(redis: RedisPool, component_key: String) -> anyhow::Result<Self> {
        let redis_key = format!("remote_config:{component_key}");

        let mut c = RuntimeConfig {
            redis,
            settings: RwLock::new(HashMap::new()),
            redis_key,
        };

        c.load().await?;

        Ok(c)
    }

    pub async fn load(&mut self) -> anyhow::Result<()> {
        let redis_config: HashMap<String, String> = self.redis.hgetall(&self.redis_key).await?;

        let mut settings = self.settings.write().await;

        for (key, value) in redis_config {
            settings.insert(key, value);
        }

        info!("starting with runtime config: {:?}", settings);
        Ok(())
    }

    pub async fn set(&self, key: String, value: String) -> anyhow::Result<()> {
        self.redis
            .hset::<(), &str, (String, String)>(&self.redis_key, (key.clone(), value.clone()))
            .await?;
        self.settings
            .write()
            .await
            .insert(key.clone(), value.clone());
        info!("updated runtime config: {key}={value}");
        Ok(())
    }

    pub async fn delete(&self, key: String) -> anyhow::Result<()> {
        self.redis
            .hdel::<(), &str, String>(&self.redis_key, key.clone())
            .await?;
        self.settings.write().await.remove(&key.clone());
        info!("updated runtime config: {key} removed");
        Ok(())
    }

    pub async fn get(&self, key: &str) -> Option<String> {
        self.settings.read().await.get(key).cloned()
    }

    pub async fn exists(&self, key: &str) -> bool {
        self.settings.read().await.contains_key(key)
    }

    pub async fn get_all(&self) -> HashMap<String, String> {
        self.settings.read().await.clone()
    }
}
