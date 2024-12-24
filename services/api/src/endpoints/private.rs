use crate::ApiContext;
use axum::{extract::State, response::Json};
use fred::interfaces::*;
use libpk::state::ShardState;
use serde::Deserialize;
use serde_json::{json, Value};
use std::collections::HashMap;

#[derive(Deserialize)]
#[serde(rename_all = "PascalCase")]
struct ClusterStats {
    pub guild_count: i32,
    pub channel_count: i32,
}

pub async fn discord_state(State(ctx): State<ApiContext>) -> Json<Value> {
    let mut shard_status = ctx
        .redis
        .hgetall::<HashMap<String, String>, &str>("pluralkit:shardstatus")
        .await
        .unwrap()
        .values()
        .map(|v| serde_json::from_str(v).expect("could not deserialize shard"))
        .collect::<Vec<ShardState>>();

    shard_status.sort_by(|a, b| a.shard_id.cmp(&b.shard_id));

    Json(json!({
        "shards": shard_status,
    }))
}

pub async fn meta(State(ctx): State<ApiContext>) -> Json<Value> {
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
        "system_count": db_stats.system_count,
        "member_count": db_stats.member_count,
        "group_count": db_stats.group_count,
        "switch_count": db_stats.switch_count,
        "message_count": db_stats.message_count,
        "guild_count": guild_count,
        "channel_count": channel_count,
    }))
}
