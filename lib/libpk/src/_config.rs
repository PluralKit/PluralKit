use config::Config;
use lazy_static::lazy_static;
use serde::Deserialize;
use std::sync::Arc;

#[derive(Deserialize, Debug)]
pub struct DiscordConfig {
    pub client_id: u32,
    pub bot_token: String,
    pub client_secret: String,
}

#[derive(Deserialize, Debug)]
pub struct DatabaseConfig {
    pub(crate) _data_db_uri: String,
    pub(crate) _messages_db_uri: String,
    pub(crate) _db_password: Option<String>,
    pub data_redis_addr: String,
}

fn _default_api_addr() -> String {
    "0.0.0.0:5000".to_string()
}

#[derive(Deserialize, Debug)]
pub struct ApiConfig {
    #[serde(default = "_default_api_addr")]
    pub addr: String,

    #[serde(default)]
    pub ratelimit_redis_addr: Option<String>,

    pub remote_url: String,
}

fn _metrics_default() -> bool {
    false
}

#[derive(Deserialize, Debug)]
pub struct PKConfig {
    pub discord: DiscordConfig,
    pub api: ApiConfig,

    #[serde(default = "_metrics_default")]
    pub run_metrics_server: bool,

    pub(crate) gelf_log_url: Option<String>,
}

lazy_static! {
    #[derive(Debug)]
    pub static ref CONFIG: Arc<PKConfig> = Arc::new(Config::builder()
    .add_source(config::Environment::with_prefix("pluralkit").separator("__"))
    .build().unwrap()
    .try_deserialize::<PKConfig>().unwrap());
}
