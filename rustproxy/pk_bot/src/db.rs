use std::str::FromStr;

use crate::{
    config::BotConfig,
    model::{PKMember, PKMemberGuild, PKMessage, PKSystem, PKSystemGuild},
};
use chrono::{DateTime, Utc};
use sqlx::{
    postgres::{PgConnectOptions, PgPoolOptions},
    ConnectOptions, FromRow, PgPool,
};
use tracing::info;

#[derive(FromRow, Debug, Default)]
pub struct MessageContext {
    // being defensive with these values - we need to be explicit with Option<T>
    // when the database might return null, and some of these don't have proper default values set
    // most of the Option<T>s can probably get removed with a few changes to the db function
    pub system_id: Option<i32>,
    pub is_deleting: Option<bool>,
    pub in_blacklist: Option<bool>,
    pub in_log_blacklist: Option<bool>,
    pub proxy_enabled: Option<bool>,
    pub last_switch: Option<i32>,
    pub last_switch_members: Option<Vec<i32>>,
    pub last_switch_timestamp: Option<DateTime<Utc>>,
    pub system_tag: Option<String>,
    pub system_guild_tag: Option<String>,
    pub tag_enabled: Option<bool>,
    pub system_avatar: Option<String>,
    pub allow_autoproxy: Option<bool>,
    pub latch_timeout: Option<i32>,
}

pub async fn get_message_context(
    pool: &PgPool,
    account_id: i64,
    guild_id: i64,
    channel_id: i64,
) -> anyhow::Result<MessageContext> {
    Ok(sqlx::query_as("select * from message_context($1, $2, $3)")
        .bind(account_id)
        .bind(guild_id)
        .bind(channel_id)
        .fetch_one(pool)
        .await?)
}

#[derive(FromRow, Debug, Clone)]
pub struct ProxyTagEntry {
    pub prefix: String,
    pub suffix: String,
    pub member_id: i32,
}

impl From<(&str, &str, i32)> for ProxyTagEntry {
    fn from((prefix, suffix, member_id): (&str, &str, i32)) -> Self {
        ProxyTagEntry {
            prefix: prefix.to_string(),
            suffix: suffix.to_string(),
            member_id,
        }
    }
}

pub async fn get_proxy_tags(pool: &PgPool, system_id: i32) -> anyhow::Result<Vec<ProxyTagEntry>> {
    Ok(sqlx::query_as("select coalesce((i.tags).prefix, '') as prefix, coalesce((i.tags).suffix, '') as suffix, member_id from (select unnest(proxy_tags) as tags, id as member_id from members where system = $1) as i;")
        .bind(system_id)
        .fetch_all(pool)
        .await?)
}

#[repr(i32)]
#[derive(sqlx::Type, Debug, Copy, Clone)]
pub enum AutoproxyMode {
    Off = 1,
    Front = 2,
    Latch = 3,
    Member = 4,
}

#[derive(FromRow, Debug, Clone)]
pub struct AutoproxyState {
    pub autoproxy_mode: AutoproxyMode,
    pub autoproxy_member: Option<i32>,
    pub last_latch_timestamp: Option<DateTime<Utc>>,
}

pub async fn get_autoproxy_state(
    pool: &PgPool,
    system_id: i32,
    guild_id: i64,
    channel_id: i64,
) -> anyhow::Result<Option<AutoproxyState>> {
    Ok(sqlx::query_as(
        "select * from autoproxy where system = $1 and guild_id = $2 and channel_id = $3;",
    )
    .bind(system_id)
    .bind(guild_id)
    .bind(channel_id)
    .fetch_optional(pool)
    .await?)
}

pub async fn get_system_by_id(pool: &PgPool, system_id: i32) -> anyhow::Result<Option<PKSystem>> {
    Ok(sqlx::query_as("select * from systems where id = $1")
        .bind(system_id)
        .fetch_optional(pool)
        .await?)
}

pub async fn get_member_by_id(pool: &PgPool, member_id: i32) -> anyhow::Result<Option<PKMember>> {
    Ok(sqlx::query_as("select * from members where id = $1")
        .bind(member_id)
        .fetch_optional(pool)
        .await?)
}

pub async fn get_system_guild(
    pool: &PgPool,
    system_id: i32,
    guild_id: i64,
) -> anyhow::Result<Option<PKSystemGuild>> {
    Ok(
        sqlx::query_as("select * from system_guild where system = $1 and guild = $2")
            .bind(system_id)
            .bind(guild_id)
            .fetch_optional(pool)
            .await?,
    )
}

pub async fn get_member_guild(
    pool: &PgPool,
    member_id: i32,
    guild_id: i64,
) -> anyhow::Result<Option<PKMemberGuild>> {
    Ok(
        sqlx::query_as("select * from member_guild where member = $1 and guild = $2")
            .bind(member_id)
            .bind(guild_id)
            .fetch_optional(pool)
            .await?,
    )
}

pub async fn insert_message(pool: &PgPool, message: PKMessage) -> anyhow::Result<()> {
    sqlx::query("insert into messages (mid, guild, channel, member, sender, original_mid) values ($1, $2, $3, $4, $5, $6)")
        .bind(message.mid)
        .bind(message.guild)
        .bind(message.channel)
        .bind(message.member_id)
        .bind(message.sender)
        .bind(message.original_mid)
        .execute(pool)
        .await?;

    Ok(())
}

pub async fn init_db(config: &BotConfig) -> anyhow::Result<PgPool> {
    info!("connecting to database");
    let options = PgConnectOptions::from_str(&config.database)
        .unwrap()
        .disable_statement_logging()
        .clone();

    let pool = PgPoolOptions::new()
        .max_connections(32)
        .connect_with(options)
        .await?;

    Ok(pool)
}
