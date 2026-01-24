use std::{collections::HashMap, time::Duration};

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
use tokio::{process::Command, sync::Mutex};

use crate::AppCtx;

pub async fn update_prometheus(ctx: AppCtx) -> anyhow::Result<()> {
    let data_ts = *BASEBACKUP_TS.lock().await.get("data").unwrap_or(&0) as f64;
    let messages_ts = *BASEBACKUP_TS.lock().await.get("messages").unwrap_or(&0) as f64;

    let now_ts = chrono::Utc::now().timestamp() as f64;

    gauge!("pluralkit_latest_backup_ts", "repo" => "data").set(data_ts);
    gauge!("pluralkit_latest_backup_ts", "repo" => "messages").set(messages_ts);

    gauge!("pluralkit_latest_backup_age", "repo" => "data").set(now_ts - data_ts);
    gauge!("pluralkit_latest_backup_age", "repo" => "messages").set(now_ts - messages_ts);

    #[derive(sqlx::FromRow)]
    struct Count {
        count: i64,
    }

    let pending_count: Count = sqlx::query_as("select count(*) from image_cleanup_pending_jobs")
        .fetch_one(&ctx.data)
        .await?;

    let count: Count = sqlx::query_as("select count(*) from image_cleanup_jobs")
        .fetch_one(&ctx.data)
        .await?;

    gauge!("pluralkit_image_cleanup_queue_length", "pending" => "true")
        .set(pending_count.count as f64);
    gauge!("pluralkit_image_cleanup_queue_length", "pending" => "false").set(count.count as f64);

    let gateway = ctx.discord.gateway().authed().await?.model().await?;

    gauge!("pluralkit_gateway_sessions_remaining")
        .set(gateway.session_start_limit.remaining as f64);
    gauge!("pluralkit_gateway_sessions_reset_after")
        .set(gateway.session_start_limit.reset_after as f64);

    Ok(())
}

lazy_static::lazy_static! {
    static ref BASEBACKUP_TS: Mutex<HashMap<String, i64>> = Mutex::new(HashMap::new());
}

pub async fn update_data_basebackup_prometheus(_: AppCtx) -> anyhow::Result<()> {
    update_basebackup_ts("data".to_string()).await
}

pub async fn update_messages_basebackup_prometheus(_: AppCtx) -> anyhow::Result<()> {
    update_basebackup_ts("messages".to_string()).await
}

async fn update_basebackup_ts(repo: String) -> anyhow::Result<()> {
    let mut env = HashMap::new();

    for (key, value) in std::env::vars() {
        if key.starts_with("AWS") {
            env.insert(key, value);
        }
    }

    env.insert(
        "WALG_S3_PREFIX".to_string(),
        format!("s3://pluralkit-backups/{repo}/"),
    );

    let output = Command::new("wal-g")
        .arg("backup-list")
        .arg("--json")
        .envs(env)
        .output()
        .await?;

    if !output.status.success() {
        // todo: we should return error here
        tracing::error!(
            status = output.status.code(),
            "failed to execute wal-g command"
        );
        return Ok(());
    }

    #[derive(serde::Deserialize)]
    struct WalgBackupInfo {
        backup_name: String,
        time: String,
        ts_parsed: Option<i64>,
    }

    let mut info =
        serde_json::from_str::<Vec<WalgBackupInfo>>(&String::from_utf8_lossy(&output.stdout))?
            .into_iter()
            .filter(|v| v.backup_name.contains("base"))
            .filter_map(|mut v| {
                chrono::DateTime::parse_from_rfc3339(&v.time)
                    .ok()
                    .map(|dt| {
                        v.ts_parsed = Some(dt.with_timezone(&chrono::Utc).timestamp());
                        v
                    })
            })
            .collect::<Vec<WalgBackupInfo>>();

    info.sort_by(|a, b| b.ts_parsed.cmp(&a.ts_parsed));

    let Some(info) = info.first() else {
        anyhow::bail!("could not find any basebackups in repo {repo}");
    };

    BASEBACKUP_TS
        .lock()
        .await
        .insert(repo, info.ts_parsed.unwrap());

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
    let mut url = cfg.gateway_url.clone();

    for idx in 0..cfg.expected_gateway_count {
        if url.contains("{clusterid}") {
            url = url.replace("{clusterid}", &idx.to_string());
        }

        let res = client.get(&url).send().await?;

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

const IMAGE_CHECK_COLUMNS: &[(&str, &str)] = &[
    ("systems", "avatar_url"),
    ("systems", "banner_image"),
    ("system_guild", "avatar_url"),
    ("members", "avatar_url"),
    ("members", "banner_image"),
    ("members", "webhook_avatar_url"),
    ("member_guild", "avatar_url"),
    ("groups", "icon"),
    ("groups", "banner_image"),
];

pub async fn queue_deleted_image_cleanup(ctx: AppCtx) -> anyhow::Result<()> {
    // if an image is present on no member, add it to the pending deletion queue
    // if it is still present on no member after 24h, actually delete it
    let mut usage_query = String::new();
    for (table, col) in IMAGE_CHECK_COLUMNS {
        usage_query.push_str(&format!(
            r#"
            and not exists (
                select 1 from {table} 
                where {col} = h.url
                or {col} like '%/' || a.system_uuid::text || '/' || a.id::text || '.%'
            )
            "#
        ));
    }

    ctx.data
        .execute(
            format!(
                r#"
            insert into image_cleanup_pending_jobs
            select a.id, a.system_uuid, now() from images_assets a
            join images_hashes h on a.image = h.hash where
            a.kind not in ('premium_banner', 'premium_avatar')
            and not exists (select from image_cleanup_pending_jobs j where j.id = a.id)
            and not exists (select from image_cleanup_jobs j where j.id = a.id)
            {}
        "#,
                usage_query
            )
            .as_str(),
        )
        .await?;

    ctx.data
        .execute(
            format!(
                r#"
            insert into image_cleanup_jobs (id, system_uuid)
            select p.id, p.system_uuid from image_cleanup_pending_jobs p
            join images_assets a on a.id = p.id
            join images_hashes h on a.image = h.hash
            where
            a.kind not in ('premium_banner', 'premium_avatar')
            and ts < now() - '24 hours'::interval
            and not exists (select from image_cleanup_jobs j where j.id = p.id)
            {}
        "#,
                usage_query
            )
            .as_str(),
        )
        .await?;

    Ok(())
}

pub async fn queue_orphaned_hash_cleanup(ctx: AppCtx) -> anyhow::Result<()> {
    let mut usage_checks = String::new();
    for (table, col) in IMAGE_CHECK_COLUMNS {
        usage_checks.push_str(&format!(
            "and not exists (select 1 from {table} where {col} = h.url) "
        ));
    }

    ctx.data
        .execute(
            format!(
                r#"
        insert into image_hash_cleanup_pending_jobs (hash, ts)
        select h.hash, now()
        from images_hashes h
        where 
            not exists (
                select 1 from images_assets a 
                where a.image = h.hash 
                or a.proxy_image = h.hash
            )
            {usage_checks}
            and not exists (select 1 from image_hash_cleanup_pending_jobs p where p.hash = h.hash)
            and not exists (select 1 from image_hash_cleanup_jobs j where j.hash = h.hash)
    "#
            )
            .as_str(),
        )
        .await?;

    ctx.data
        .execute(
            format!(
                r#"
        insert into image_hash_cleanup_jobs (hash)
        select p.hash
        from image_hash_cleanup_pending_jobs p
        join images_hashes h ON h.hash = p.hash
        where
            p.ts < now() - '24 hours'::interval
            and not exists (
                select 1 from images_assets a 
                where a.image = h.hash 
                or a.proxy_image = h.hash
            )
            {usage_checks}
            and not exists (select 1 from image_hash_cleanup_jobs j where j.hash = p.hash)
    "#
            )
            .as_str(),
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

    let cfg = config
        .scheduled_tasks
        .as_ref()
        .expect("missing scheduled_tasks config");

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
            tracing::info!("Query: {}", $q);
            let resp = client
                .get(format!("{}/api/v1/query?query={}", cfg.prometheus_url, $q))
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
