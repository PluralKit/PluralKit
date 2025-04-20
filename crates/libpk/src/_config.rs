use config::Config;
use lazy_static::lazy_static;
use serde::Deserialize;
use std::sync::Arc;

use twilight_model::id::{marker::UserMarker, Id};

#[derive(Clone, Deserialize, Debug)]
pub struct ClusterSettings {
    pub node_id: u32,
    pub total_shards: u32,
    pub total_nodes: u32,
}

#[derive(Deserialize, Debug)]
pub struct DiscordConfig {
    pub client_id: Id<UserMarker>,
    pub bot_token: String,
    pub client_secret: String,
    pub max_concurrency: u32,
    #[serde(default)]
    pub cluster: Option<ClusterSettings>,
    pub api_base_url: Option<String>,

    #[serde(default = "_default_api_addr")]
    pub cache_api_addr: String,

    #[serde(default)]
    pub gateway_target: Option<String>,
}

#[derive(Deserialize, Debug)]
pub struct DatabaseConfig {
    pub(crate) data_db_uri: String,
    pub(crate) data_db_max_connections: Option<u32>,
    pub(crate) data_db_min_connections: Option<u32>,
    pub(crate) messages_db_uri: Option<String>,
    pub(crate) stats_db_uri: Option<String>,
    pub(crate) db_password: Option<String>,
    pub data_redis_addr: String,
}

fn _default_api_addr() -> String {
    "[::]:5000".to_string()
}

#[derive(Deserialize, Clone, Debug)]
pub struct ApiConfig {
    #[serde(default = "_default_api_addr")]
    pub addr: String,

    #[serde(default)]
    pub ratelimit_redis_addr: Option<String>,

    pub remote_url: String,

    #[serde(default)]
    pub temp_token2: Option<String>,
}

#[derive(Deserialize, Clone, Debug)]
pub struct AvatarsConfig {
    pub s3: S3Config,
    pub cdn_url: String,

    #[serde(default = "_default_api_addr")]
    pub bind_addr: String,

    #[serde(default)]
    pub migrate_worker_count: u32,

    #[serde(default)]
    pub cloudflare_zone_id: Option<String>,
    #[serde(default)]
    pub cloudflare_token: Option<String>,
}

#[derive(Deserialize, Clone, Debug)]
pub struct S3Config {
    pub bucket: String,
    pub application_id: String,
    pub application_key: String,
    pub endpoint: String,
}

#[derive(Deserialize, Debug)]
pub struct ScheduledTasksConfig {
    pub set_guild_count: bool,
    pub expected_gateway_count: usize,
    pub gateway_url: String,
}

fn _metrics_default() -> bool {
    false
}
fn _json_log_default() -> bool {
    false
}

#[derive(Deserialize, Debug)]
pub struct PKConfig {
    pub db: DatabaseConfig,

    #[serde(default)]
    pub discord: Option<DiscordConfig>,
    #[serde(default)]
    pub api: Option<ApiConfig>,
    #[serde(default)]
    pub avatars: Option<AvatarsConfig>,
    #[serde(default)]
    pub scheduled_tasks: Option<ScheduledTasksConfig>,

    #[serde(default = "_metrics_default")]
    pub run_metrics_server: bool,

    #[serde(default = "_json_log_default")]
    pub(crate) json_log: bool,

    #[serde(default)]
    pub runtime_config_key: Option<String>,

    #[serde(default)]
    pub sentry_url: Option<String>,
}

impl PKConfig {
    pub fn api(self) -> ApiConfig {
        self.api.expect("missing api config")
    }

    pub fn discord_config(self) -> DiscordConfig {
        self.discord.expect("missing discord config")
    }
}

// todo: consider passing this down instead of making it global
// especially since we have optional discord/api/avatars/etc config
lazy_static! {
    #[derive(Debug)]
    pub static ref CONFIG: Arc<PKConfig> = {
        // hacks
        if let Ok(var) = std::env::var("NOMAD_ALLOC_INDEX")
            && std::env::var("pluralkit__discord__cluster__total_nodes").is_ok() {
            std::env::set_var("pluralkit__discord__cluster__node_id", var);
        }
        if let Ok(var) = std::env::var("STATEFULSET_NAME_FOR_INDEX")
            && std::env::var("pluralkit__discord__cluster__total_nodes").is_ok() {
            std::env::set_var("pluralkit__discord__cluster__node_id", var.split("-").last().unwrap());
        }

        Arc::new(Config::builder()
        .add_source(config::Environment::with_prefix("pluralkit").separator("__"))
        .build().unwrap()
        .try_deserialize::<PKConfig>().unwrap())
    };
}
