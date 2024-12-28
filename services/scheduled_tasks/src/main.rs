use chrono::{TimeDelta, Timelike};
use fred::prelude::RedisPool;
use sqlx::PgPool;
use std::time::Duration;
use tokio::task::JoinSet;
use tracing::{error, info};

mod tasks;
use tasks::*;

#[derive(Clone)]
pub struct AppCtx {
    pub data: PgPool,
    pub messages: PgPool,
    pub stats: PgPool,
    pub redis: RedisPool,
}

libpk::main!("scheduled_tasks");
async fn real_main() -> anyhow::Result<()> {
    let ctx = AppCtx {
        data: libpk::db::init_data_db().await?,
        messages: libpk::db::init_messages_db().await?,
        stats: libpk::db::init_stats_db().await?,
        redis: libpk::db::init_redis().await?,
    };

    info!("starting scheduled tasks runner");

    let mut set = JoinSet::new();

    // i couldn't be bothered to figure out the types of passing in an async
    // function to another function... so macro it is
    macro_rules! doforever {
        ($timeout:expr, $desc:expr, $fn:ident) => {
            let ctx = ctx.clone();
            set.spawn(tokio::spawn(async move {
                loop {
                    let ctx = ctx.clone();
                    wait_interval($timeout).await;
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

    doforever!(10, "prometheus updater", update_prometheus);
    doforever!(60, "database stats updater", update_db_meta);
    doforever!(600, "message stats updater", update_db_message_meta);
    doforever!(60, "discord stats updater", update_discord_stats);
    doforever!(
        1800,
        "queue deleted image cleanup job",
        queue_deleted_image_cleanup
    );

    set.join_next()
        .await
        .ok_or(anyhow::anyhow!("could not join_next"))???;

    Ok(())
}

// cron except not really
async fn wait_interval(interval_secs: u32) {
    let now = chrono::Utc::now();

    // for now, only supports hardcoded intervals
    let next_iter_time = match interval_secs {
        // 10 sec
        // at every [x]0th second (00, 10, 20, 30, 40, 50)
        10 => {
            let mut minute = 0;
            let mut second = (now.second() - (now.second() % 10)) + 10;
            if second == 60 {
                minute += 1;
                second = 0;
            }
            now.checked_add_signed(TimeDelta::minutes(minute))
                .expect("invalid time")
                .with_second(second)
                .expect("invalid time")
        }
        // 1 minute
        // every minute at :00 seconds
        60 => now
            .checked_add_signed(TimeDelta::minutes(1))
            .expect("invalid time")
            .with_second(0)
            .expect("invalid time"),
        // 10 minutes
        // at every [x]0 minute (00, 10, 20, 30, 40, 50)
        600 => {
            let mut minute = (now.minute() + 10) % 10;
            let mut hour = 0;
            if minute == 60 {
                minute = 0;
                hour = 1;
            }

            now.checked_add_signed(TimeDelta::hours(hour))
                .expect("invalid time")
                .with_minute(minute)
                .expect("invalid time")
        }
        // 30 minutes
        // at :00 and :30
        1800 => {
            let mut minute = (now.minute() + 30) % 30;
            let mut hour = 0;
            if minute == 60 {
                minute = 0;
                hour = 1;
            }

            now.checked_add_signed(TimeDelta::hours(hour))
                .expect("invalid time")
                .with_minute(minute)
                .expect("invalid time")
        }

        _ => unreachable!(),
    };

    let dur = next_iter_time - now;

    tokio::time::sleep(Duration::from_secs(dur.num_seconds() as u64)).await;
}
