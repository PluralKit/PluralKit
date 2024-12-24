#[derive(serde::Serialize, serde::Deserialize, Clone, Default)]
pub struct ShardState {
    pub shard_id: i32,
    pub up: bool,
    pub disconnection_count: i32,
    /// milliseconds
    pub latency: i32,
    /// unix timestamp
    pub last_heartbeat: i32,
    pub last_connection: i32,
    pub cluster_id: Option<i32>,
}
