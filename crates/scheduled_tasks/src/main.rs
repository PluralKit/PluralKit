use std::sync::Arc;

use chrono::Utc;
use croner::Cron;
use fred::prelude::RedisPool;
use sqlx::PgPool;
use tokio::task::JoinSet;
use tracing::{debug, error, info};

mod tasks;
use tasks::*;

#[derive(Clone)]
pub struct AppCtx {
    pub data: PgPool,
    pub messages: PgPool,
    pub stats: PgPool,
    pub redis: RedisPool,

    pub discord: Arc<twilight_http::Client>,
}

libpk::main!("scheduled_tasks");
async fn real_main() -> anyhow::Result<()> {
    let mut client_builder = twilight_http::Client::builder().token(
        libpk::config
            .discord
            .as_ref()
            .expect("missing discord config")
            .bot_token
            .clone(),
    );

    if let Some(base_url) = libpk::config
        .discord
        .as_ref()
        .expect("missing discord config")
        .api_base_url
        .clone()
    {
        client_builder = client_builder.proxy(base_url, true).ratelimiter(None);
    }

    let ctx = AppCtx {
        data: libpk::db::init_data_db().await?,
        messages: libpk::db::init_messages_db().await?,
        stats: libpk::db::init_stats_db().await?,
        redis: libpk::db::init_redis().await?,

        discord: Arc::new(client_builder.build()),
    };

    info!("starting scheduled tasks runner");

    let mut set = JoinSet::new();

    // i couldn't be bothered to figure out the types of passing in an async
    // function to another function... so macro it is
    macro_rules! doforever {
        ($cron:expr, $desc:expr, $fn:ident) => {
            let ctx = ctx.clone();
            let cron = Cron::new($cron)
                .with_seconds_optional()
                .parse()
                .expect("invalid cron");
            set.spawn(tokio::spawn(async move {
                loop {
                    let ctx = ctx.clone();
                    let next_iter_time = cron.find_next_occurrence(&Utc::now(), false).unwrap();
                    debug!("next execution of {} at {:?}", $desc, next_iter_time);
                    let dur = next_iter_time - Utc::now();
                    tokio::time::sleep(dur.to_std().unwrap()).await;

                    info!("running {}", $desc);
                    let before = std::time::Instant::now();
                    if let Err(error) = $fn(ctx).await {
                        error!("failed to run {}: {}", $desc, error);
                        // sentry
                    }
                    let duration = before.elapsed();
                    info!("ran {} in {duration:?}", $desc);
                    // add prometheus log
                }
            }))
        };
    }

    // every 10 seconds
    doforever!(
        "0,10,20,30,40,50 * * * * *",
        "prometheus updater",
        update_prometheus
    );
    // every minute
    doforever!("* * * * *", "database stats updater", update_db_meta);
    // every 10 minutes
    doforever!(
        "0,10,20,30,40,50 * * * *",
        "message stats updater",
        update_db_message_meta
    );
    doforever!("* * * * *", "discord stats updater", update_discord_stats);
    // on :00 and :30
    doforever!(
        "0,30 * * * *",
        "queue deleted image cleanup job",
        queue_deleted_image_cleanup
    );
    doforever!("0,30 * * * * *", "stats api updater", update_stats_api);

    set.join_next()
        .await
        .ok_or(anyhow::anyhow!("could not join_next"))???;

    Ok(())
}
