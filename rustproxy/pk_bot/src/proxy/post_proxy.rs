use crate::db;
use crate::model::PKMessage;
use futures::TryFutureExt;
use sqlx::PgPool;
use tracing::error;
use twilight_http::Client;

#[derive(Debug, Clone)]
pub struct ProxyResult {
    pub guild_id: u64,
    pub channel_id: u64,
    pub proxy_message_id: u64,
    pub original_message_id: u64,
    pub sender: u64,
    pub member_id: i32,
}

pub async fn handle_post_proxy(
    http: &Client,
    pool: &PgPool,
    res: &ProxyResult,
) -> anyhow::Result<()> {
    // todo: log channel

    let _ = futures::join!(
        delete_original_message(http, res)
            .inspect_err(|e| error!("error deleting original message: {}", e)),
        insert_message_in_db(pool, res)
            .inspect_err(|e| error!("error deleting original message: {}", e))
    );

    Ok(())
}

async fn delete_original_message(http: &Client, res: &ProxyResult) -> anyhow::Result<()> {
    // todo: sleep some amount
    // (do we still need to do that or did discord fix that client bug?)
    http.delete_message(
        res.channel_id.try_into().unwrap(),
        res.original_message_id.try_into().unwrap(),
    )
    .exec()
    .await?;

    Ok(())
}

async fn insert_message_in_db(pool: &PgPool, res: &ProxyResult) -> anyhow::Result<()> {
    db::insert_message(
        pool,
        PKMessage {
            mid: res.proxy_message_id as i64,
            original_mid: Some(res.original_message_id as i64),
            sender: res.sender as i64,
            guild: Some(res.guild_id as i64),
            channel: res.channel_id as i64,
            member_id: res.member_id,
        },
    )
    .await?;

    Ok(())
}

#[cfg(test)]
mod tests {}
