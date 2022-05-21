use std::{str::FromStr, time::SystemTime};

use crate::config;
use anyhow::Context;
use deadpool_postgres::{Manager, ManagerConfig, Pool, RecyclingMethod};
use tokio_postgres::{self, types::FromSql, Row};
use twilight_model::id::Id;
use twilight_model::id::marker::ChannelMarker;

pub async fn init_db(cfg: &config::BotConfig) -> anyhow::Result<Pool> {
    let pg_config = tokio_postgres::config::Config::from_str(&cfg.database)
        .context("could not parse connection string")?;

    let mgr_config = ManagerConfig {
        recycling_method: RecyclingMethod::Fast,
    };
    let mgr = Manager::from_config(pg_config, tokio_postgres::NoTls, mgr_config);
    let pool = Pool::builder(mgr)
        .max_size(16)
        .build()
        .context("could not initialize pool")?;
    Ok(pool)
}

pub async fn get_message_context(
    pool: &Pool,
    account_id: u64,
    guild_id: u64,
    channel_id: u64,
) -> anyhow::Result<Option<MessageContext>> {
    let client = pool.get().await?;
    let stmt = client
        .prepare_cached("select * from message_context($1, $2, $3)")
        .await?;
    let result = client
        .query_opt(
            &stmt,
            &[
                &(account_id as i64),
                &(guild_id as i64),
                &(channel_id as i64),
            ],
        )
        .await
        .context("could not fetch message context")?;

    Ok(result.map(parse_message_context))
}

pub async fn get_proxy_members(
    pool: &Pool,
    account_id: u64,
    guild_id: u64,
) -> anyhow::Result<Vec<ProxyMember>> {
    let client = pool.get().await?;
    let stmt = client
        .prepare_cached("select * from proxy_members($1, $2)")
        .await?;
    let result = client
        .query(&stmt, &[&(account_id as i64), &(guild_id as i64)])
        .await
        .context("could not fetch proxy members")?;

    Ok(result.into_iter().map(parse_proxy_member).collect())
}

#[derive(Debug)]
pub struct MessageContext {
    pub system_id: Option<i32>,
    pub log_channel: Option<Id<ChannelMarker>>,
    pub in_blacklist: bool,
    pub in_log_blacklist: bool,
    pub log_cleanup_enabled: bool,
    pub proxy_enabled: bool,
    pub last_switch: Option<i32>,
    pub last_switch_members: Option<Vec<i32>>,
    pub last_switch_timestamp: Option<SystemTime>,
    pub system_tag: Option<String>,
    pub system_guild_tag: Option<String>,
    pub tag_enabled: bool,
    pub system_avatar: Option<String>,
    pub allow_autoproxy: bool,
    pub latch_timeout: Option<i32>,
}

#[derive(Debug, FromSql)]
#[postgres(name = "proxy_tag")]
pub struct ProxyTag {
    pub prefix: Option<String>,
    pub suffix: Option<String>,
}

#[derive(Debug)]
pub struct ProxyMember {
    pub id: i32,
    pub proxy_tags: Vec<ProxyTag>,
    pub keep_proxy: bool,
    pub server_name: Option<String>,
    pub display_name: Option<String>,
    pub name: String,
    pub server_avatar: Option<String>,
    pub avatar: Option<String>,
    pub allow_autoproxy: bool,
    pub color: Option<String>,
}

fn parse_message_context(row: Row) -> MessageContext {
    MessageContext {
        system_id: row.get("system_id"),
        log_channel: to_channel_id_opt(row.get("log_channel")),
        in_blacklist: row.get::<_, Option<_>>("in_blacklist").unwrap_or(false),
        in_log_blacklist: row.get::<_, Option<_>>("in_log_blacklist").unwrap_or(false),
        log_cleanup_enabled: row.get("log_cleanup_enabled"),
        proxy_enabled: row.get("proxy_enabled"),
        last_switch: row.get("last_switch"),
        last_switch_members: row.get("last_switch_members"),
        last_switch_timestamp: row.get("last_switch_timestamp"),
        system_tag: row.get("system_tag"),
        system_guild_tag: row.get("system_guild_tag"),
        tag_enabled: row.get("tag_enabled"),
        system_avatar: row.get("system_avatar"),
        allow_autoproxy: row.get("allow_autoproxy"),
        latch_timeout: row.get("latch_timeout"),
    }
}

fn parse_proxy_member(row: Row) -> ProxyMember {
    ProxyMember {
        id: row.get("id"),
        proxy_tags: row.get("proxy_tags"),
        keep_proxy: row.get("keep_proxy"),
        server_name: row.get("server_name"),
        display_name: row.get("display_name"),
        name: row.get("name"),
        server_avatar: row.get("server_avatar"),
        avatar: row.get("avatar"),
        allow_autoproxy: row.get("allow_autoproxy"),
        color: row.get("color"),
    }
}

fn to_channel_id_opt(id: Option<i64>) -> Option<Id<ChannelMarker>> {
    id.and_then(|x| Some(Id::<ChannelMarker>::new(x as u64)))
}
