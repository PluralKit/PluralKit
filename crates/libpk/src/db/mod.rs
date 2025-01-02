use fred::clients::RedisPool;
use sqlx::postgres::{PgConnectOptions, PgPool, PgPoolOptions};
use std::str::FromStr;
use tracing::info;

pub mod repository;
pub mod types;

pub async fn init_redis() -> anyhow::Result<RedisPool> {
    info!("connecting to redis");
    let redis = RedisPool::new(
        fred::types::RedisConfig::from_url_centralized(crate::config.db.data_redis_addr.as_ref())
            .expect("redis url is invalid"),
        None,
        None,
        Some(Default::default()),
        10,
    )?;

    let redis_handle = redis.connect_pool();
    tokio::spawn(async move { redis_handle });

    Ok(redis)
}

pub async fn init_data_db() -> anyhow::Result<PgPool> {
    info!("connecting to database");

    let mut options = PgConnectOptions::from_str(&crate::config.db.data_db_uri)?;

    if let Some(password) = crate::config.db.db_password.clone() {
        options = options.password(&password);
    }

    let mut pool = PgPoolOptions::new();

    if let Some(max_conns) = crate::config.db.data_db_max_connections {
        pool = pool.max_connections(max_conns);
    }

    if let Some(min_conns) = crate::config.db.data_db_min_connections {
        pool = pool.min_connections(min_conns);
    }

    Ok(pool.connect_with(options).await?)
}

pub async fn init_messages_db() -> anyhow::Result<PgPool> {
    info!("connecting to messages database");

    let mut options = PgConnectOptions::from_str(
        &crate::config
            .db
            .messages_db_uri
            .as_ref()
            .expect("missing messages db uri"),
    )?;

    if let Some(password) = crate::config.db.db_password.clone() {
        options = options.password(&password);
    }

    let mut pool = PgPoolOptions::new();

    if let Some(max_conns) = crate::config.db.data_db_max_connections {
        pool = pool.max_connections(max_conns);
    }

    if let Some(min_conns) = crate::config.db.data_db_min_connections {
        pool = pool.min_connections(min_conns);
    }

    Ok(pool.connect_with(options).await?)
}

pub async fn init_stats_db() -> anyhow::Result<PgPool> {
    info!("connecting to stats database");

    let mut options = PgConnectOptions::from_str(
        &crate::config
            .db
            .stats_db_uri
            .as_ref()
            .expect("missing messages db uri"),
    )?;

    if let Some(password) = crate::config.db.db_password.clone() {
        options = options.password(&password);
    }

    Ok(PgPoolOptions::new()
        .max_connections(1)
        .min_connections(1)
        .connect_with(options)
        .await?)
}
