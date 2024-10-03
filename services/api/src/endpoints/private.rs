use crate::ApiContext;
use axum::{extract::State, response::Json};
use fred::interfaces::*;
use libpk::proto::ShardState;
use prost::Message;
use serde::Deserialize;
use serde_json::{json, Value};
use std::collections::HashMap;

#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
struct ClusterStats {
    pub guild_count: i32,
    pub channel_count: i32,
}

pub async fn meta(State(ctx): State<ApiContext>) -> Json<Value> {
    let shard_status = ctx
        .redis
        .hgetall::<HashMap<String, Vec<u8>>, &str>("pluralkit:shardstatus")
        .await
        .unwrap()
        .values()
        .map(|v| ShardState::decode(v.as_slice()).unwrap())
        .collect::<Vec<ShardState>>();

    let cluster_stats = ctx
        .redis
        .hgetall::<HashMap<String, String>, &str>("pluralkit:cluster_stats")
        .await
        .unwrap()
        .values()
        .map(|v| serde_json::from_str(v).unwrap())
        .collect::<Vec<ClusterStats>>();

    let db_stats = libpk::db::repository::get_stats(&ctx.db).await.unwrap();

    let guild_count: i32 = cluster_stats.iter().map(|v| v.guild_count).sum();
    let channel_count: i32 = cluster_stats.iter().map(|v| v.channel_count).sum();

    Json(json!({
        "shards": shard_status,
        "stats": {
            "system_count": db_stats.system_count,
            "member_count": db_stats.member_count,
            "group_count": db_stats.group_count,
            "switch_count": db_stats.switch_count,
            "message_count": db_stats.message_count,
            "guild_count": guild_count,
            "channel_count": channel_count,
        }
    }))
}
