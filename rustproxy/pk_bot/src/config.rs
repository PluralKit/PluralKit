use config::{Config, Environment, File, FileFormat};
use serde::Deserialize;

#[derive(Deserialize, Debug)]
pub struct BotConfig {
    pub token: String,

    pub max_concurrency: Option<u64>,
    pub database: String,
    pub redis_addr: Option<String>,
    pub redis_gateway_queue_addr: Option<String>,
    pub shard_count: Option<u64>,
}

// todo: should this be a once_cell::Lazy global const or something
pub fn load_config() -> anyhow::Result<BotConfig> {
    let builder = Config::builder()
        .add_source(Environment::default())
        .add_source(File::new("config", FileFormat::Toml));

    Ok(builder.build()?.try_deserialize()?)
}
