use config::{self, Config};
use serde::Deserialize;

#[derive(Deserialize, Debug)]
pub struct BotConfig {
    pub token: String,
    pub max_concurrency: u64,
    pub database: String,
    pub redis_addr: String,
    pub redis_gateway_queue_addr: String,
    pub shard_count: u64
}

pub fn load_config() -> BotConfig {
    let mut settings = Config::default();
    settings.merge(config::File::with_name("config")).unwrap();
    settings
        .merge(config::Environment::with_prefix("PluralKit"))
        .unwrap();

    settings.try_into::<BotConfig>().unwrap()
}
