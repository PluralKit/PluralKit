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

    let gateway = ctx.discord.gateway().authed().await?.model().await?;

    gauge!("pluralkit_gateway_sessions_remaining")
        .set(gateway.session_start_limit.remaining as f64);
    gauge!("pluralkit_gateway_sessions_reset_after")
        .set(gateway.session_start_limit.reset_after as f64);

    Ok(())
}

pub async fn update_db_meta(ctx: AppCtx) -> anyhow::Result<()> {
    ctx.data
        .execute(
            r#"
            update info set
                system_count = (select count(*) from systems),
                member_count = (select count(*) from members),
                group_count = (select count(*) from groups),
                switch_count = (select count(*) from switches)
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

pub async fn update_stats_api(ctx: AppCtx) -> anyhow::Result<()> {
    let client = ClientBuilder::new()
        .connect_timeout(Duration::from_secs(3))
        .timeout(Duration::from_secs(3))
        .build()
        .expect("error making client");

    #[derive(serde::Deserialize, Debug)]
    struct PrometheusResult {
        data: PrometheusResultData,
    }
    #[derive(serde::Deserialize, Debug)]
    struct PrometheusResultData {
        result: Vec<PrometheusData>,
    }
    #[derive(serde::Deserialize, Debug)]
    struct PrometheusData {
        value: Vec<serde_json::Value>,
    }

    macro_rules! prom_instant_query {
        ($t:ty, $q:expr) => {{
            let resp = client
                .get(format!(
                    "http://vm.svc.pluralkit.net/select/0/prometheus/api/v1/query?query={}",
                    $q
                ))
                .send()
                .await?;

            let data = resp.json::<PrometheusResult>().await?;

            let error_handler = || anyhow::anyhow!("missing data at {}", $q);

            data.data
                .result
                .get(0)
                .ok_or_else(error_handler)?
                .value
                .clone()
                .get(1)
                .ok_or_else(error_handler)?
                .as_str()
                .ok_or_else(error_handler)?
                .parse::<$t>()?
        }};
        ($t:ty, $q:expr, $wrap:expr) => {{
            let val = prom_instant_query!($t, $q);
            let val = (val * $wrap).round() / $wrap;
            format!("{:.2}", val).parse::<f64>()?
        }};
    }

    #[derive(serde::Serialize, sqlx::FromRow)]
    struct DbStats {
        systems: i64,
        members: i64,
        groups: i64,
        switches: i64,
        messages: i64,
        messages_24h: i64,
        guilds: i64,
        channels: i64,
    }

    let db_stats: DbStats = sqlx::query_as(r#"
        select
            t1.value as systems,
            t2.value as members,
            t3.value as groups,
            t4.value as switches,
            t5.value as messages,
            (t5.value - t6.value) as messages_24h,
            t7.value as guilds,
            t8.value as channels
        from
            (select value from systems order by timestamp desc limit 1) as t1,
            (select value from members order by timestamp desc limit 1) as t2,
            (select value from groups order by timestamp desc limit 1) as t3,
            (select value from switches order by timestamp desc limit 1) as t4,
            (select value from messages order by timestamp desc limit 1) as t5,
            (select value from messages where timestamp > now() - interval '1 day' order by timestamp asc limit 1) as t6,
            (select value from guilds order by timestamp desc limit 1) as t7,
            (select value from channels order by timestamp desc limit 1) as t8
    "#).fetch_one(&ctx.stats).await?;

    let data = serde_json::json!({
        "db": db_stats,
        "prom": {
            "messages_1m": prom_instant_query!(f32, "sum(bot__messages_processed_rate1m)", 10.0),
            "messages_15m": prom_instant_query!(f32, "sum(bot__messages_processed_rate15m)", 10.0),
            "proxy_1m": prom_instant_query!(f32, "sum(bot__messages_proxied_rate1m)", 10.0),
            "proxy_15m": prom_instant_query!(f32, "sum(bot__messages_proxied_rate15m)", 10.0),
            "commands_1m": prom_instant_query!(f32, "sum(bot__commands_run_rate1m)", 10.0),
            "commands_15m": prom_instant_query!(f32, "sum(bot__commands_run_rate15m)", 10.0),
            "cpu_total_cores": prom_instant_query!(usize, "sum(host_physical_cpus)"),
            "cpu_total_threads": prom_instant_query!(usize, "sum(host_logical_cpus)"),
            "cpu_used": prom_instant_query!(f32, "100 * ((sum(host_logical_cpus) - sum(rate(host_cpu_seconds_total{mode=\"idle\"}[1m]))) / sum(host_logical_cpus)) * sum(host_logical_cpus)", 10.0),
            "memory_total": prom_instant_query!(i64, "sum(host_memory_total_bytes)").to_string(),
            "memory_used": prom_instant_query!(i64, "sum(host_memory_total_bytes) - sum(host_memory_available_bytes)").to_string(),
            "nirn_proxy_rps": prom_instant_query!(f32, "sum(rate(nirn_proxy_requests_count))", 10.0),
            "nirn_proxy_latency_p90": prom_instant_query!(f32, "histogram_quantile(0.9, sum(rate(nirn_proxy_requests_bucket[5m])) by (le))", 1000.0),
            "nirn_proxy_latency_p99": prom_instant_query!(f32, "histogram_quantile(0.99, sum(rate(nirn_proxy_requests_bucket[5m])) by (le))", 1000.0),
            "shard_latency_average": prom_instant_query!(f32, "avg(pluralkit_gateway_shard_latency)", 10.0),
        }
    });

    ctx.redis
        .set::<(), &str, String>(
            "statsapi",
            serde_json::to_string(&data).expect("should not fail"),
            Some(fred::types::Expiration::EX(60)),
            None,
            false,
        )
        .await?;

    Ok(())
}
