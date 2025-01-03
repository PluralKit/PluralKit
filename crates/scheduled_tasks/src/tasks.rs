use std::time::Duration;

use anyhow::anyhow;
use fred::prelude::KeysInterface;
use libpk::{
    config,
    db::repository::{get_stats, insert_stats},
};
use metrics::gauge;
use num_format::{Locale, ToFormattedString};
use reqwest::ClientBuilder;
use sqlx::Executor;

use crate::AppCtx;

pub async fn update_prometheus(ctx: AppCtx) -> anyhow::Result<()> {
    #[derive(sqlx::FromRow)]
    struct Count {
        count: i64,
    }
    let count: Count = sqlx::query_as("select count(*) from image_cleanup_jobs")
        .fetch_one(&ctx.data)
        .await?;

    gauge!("pluralkit_image_cleanup_queue_length").set(count.count as f64);

    // todo: remaining shard session_start_limit
    Ok(())
}

pub async fn update_db_meta(ctx: AppCtx) -> anyhow::Result<()> {
    ctx.data
        .execute(
            r#"
            update info set
                system_count = (select count(*) from systems),
                member_count = (select count(*) from systems),
                group_count = (select count(*) from systems),
                switch_count = (select count(*) from systems)
        "#,
        )
        .await?;
    let new_stats = get_stats(&ctx.data).await?;
    insert_stats(&ctx.stats, "systems", new_stats.system_count).await?;
    insert_stats(&ctx.stats, "members", new_stats.member_count).await?;
    insert_stats(&ctx.stats, "groups", new_stats.group_count).await?;
    insert_stats(&ctx.stats, "switches", new_stats.switch_count).await?;
    Ok(())
}

pub async fn update_db_message_meta(ctx: AppCtx) -> anyhow::Result<()> {
    #[derive(sqlx::FromRow)]
    struct MessageCount {
        count: i64,
    }
    let message_count: MessageCount = sqlx::query_as("select count(*) from messages")
        .fetch_one(&ctx.messages)
        .await?;
    sqlx::query("update info set message_count = $1")
        .bind(message_count.count)
        .execute(&ctx.data)
        .await?;
    insert_stats(&ctx.stats, "messages", message_count.count).await?;

    Ok(())
}

pub async fn update_discord_stats(ctx: AppCtx) -> anyhow::Result<()> {
    let client = ClientBuilder::new()
        .connect_timeout(Duration::from_secs(3))
        .timeout(Duration::from_secs(3))
        .build()
        .expect("error making client");

    let cfg = config
        .scheduled_tasks
        .as_ref()
        .expect("missing scheduled_tasks config");

    #[derive(serde::Deserialize)]
    struct GatewayStatus {
        up: bool,
        guild_count: i64,
        channel_count: i64,
    }

    let mut guild_count = 0;
    let mut channel_count = 0;

    for idx in 0..cfg.expected_gateway_count {
        let res = client
            .get(format!("http://cluster{idx}.{}/stats", cfg.gateway_url))
            .send()
            .await?;

        let stat: GatewayStatus = res.json().await?;

        if !stat.up {
            return Err(anyhow!("cluster {idx} is not up"));
        }

        guild_count += stat.guild_count;
        channel_count += stat.channel_count;
    }

    insert_stats(&ctx.stats, "guilds", guild_count).await?;
    insert_stats(&ctx.stats, "channels", channel_count).await?;

    if cfg.set_guild_count {
        ctx.redis
            .set::<(), &str, String>(
                "pluralkit:botstatus",
                format!(
                    "in {} servers",
                    guild_count.to_formatted_string(&Locale::en)
                ),
                None,
                None,
                false,
            )
            .await?;
    }

    Ok(())
}

pub async fn queue_deleted_image_cleanup(ctx: AppCtx) -> anyhow::Result<()> {
    // todo: we want to delete immediately when system is deleted, but after a
    // delay if member is deleted
    ctx.data
        .execute(
            r#"
insert into image_cleanup_jobs
select id, now() from images where
        not exists (select from image_cleanup_jobs j where j.id = images.id)
    and not exists (select from systems where avatar_url = images.url)
    and not exists (select from systems where banner_image = images.url)
    and not exists (select from system_guild where avatar_url = images.url)

    and not exists (select from members where avatar_url = images.url)
    and not exists (select from members where banner_image = images.url)
    and not exists (select from members where webhook_avatar_url = images.url)
    and not exists (select from member_guild where avatar_url = images.url)

    and not exists (select from groups where icon = images.url)
    and not exists (select from groups where banner_image = images.url);
        "#,
        )
        .await?;
    Ok(())
}
