use chrono::Timelike;
use fred::{clients::RedisPool, interfaces::*};
use signal_hook::{
    consts::{SIGINT, SIGTERM},
    iterator::Signals,
};
use std::{
    sync::{mpsc::channel, Arc},
    time::Duration,
    vec::Vec,
};
use tokio::task::JoinSet;
use tracing::{info, warn};
use twilight_gateway::{MessageSender, ShardId};
use twilight_model::gateway::payload::outgoing::UpdatePresence;

mod cache_api;
mod discord;
mod logger;

libpk::main!("gateway");
async fn real_main() -> anyhow::Result<()> {
    let (shutdown_tx, shutdown_rx) = channel::<()>();
    let shutdown_tx = Arc::new(shutdown_tx);

    let redis = libpk::db::init_redis().await?;

    let shard_state = discord::shard_state::new(redis.clone());
    let cache = Arc::new(discord::cache::new());

    let shards = discord::gateway::create_shards(redis.clone())?;

    let (event_tx, _event_rx) = channel();

    let mut senders = Vec::new();
    let mut signal_senders = Vec::new();

    let mut set = JoinSet::new();
    for shard in shards {
        senders.push((shard.id(), shard.sender()));
        signal_senders.push(shard.sender());
        set.spawn(tokio::spawn(discord::gateway::runner(
            shard,
            event_tx.clone(),
            shard_state.clone(),
            cache.clone(),
        )));
    }

    set.spawn(tokio::spawn(
        async move { scheduled_task(redis, senders).await },
    ));

    // todo: probably don't do it this way
    let api_shutdown_tx = shutdown_tx.clone();
    set.spawn(tokio::spawn(async move {
        match cache_api::run_server(cache).await {
            Err(error) => {
                tracing::error!(?error, "failed to serve cache api");
                let _ = api_shutdown_tx.send(());
            }
            _ => unreachable!(),
        }
    }));

    let mut signals = Signals::new(&[SIGINT, SIGTERM])?;

    set.spawn(tokio::spawn(async move {
        for sig in signals.forever() {
            info!("received signal {:?}", sig);

            let presence = UpdatePresence {
                op: twilight_model::gateway::OpCode::PresenceUpdate,
                d: discord::gateway::presence("Restarting... (please wait)", true),
            };

            for sender in signal_senders.iter() {
                let presence = presence.clone();
                let _ = sender.command(&presence);
            }

            let _ = shutdown_tx.send(());
            break;
        }
    }));

    let _ = shutdown_rx.recv();

    // sleep 500ms to allow everything to clean up properly
    tokio::time::sleep(Duration::from_millis(500)).await;

    set.abort_all();

    info!("gateway exiting, have a nice day!");

    Ok(())
}

async fn scheduled_task(redis: RedisPool, senders: Vec<(ShardId, MessageSender)>) {
    loop {
        tokio::time::sleep(Duration::from_secs(
            (60 - chrono::offset::Utc::now().second()).into(),
        ))
        .await;
        info!("running per-minute scheduled tasks");

        let status: Option<String> = match redis.get("pluralkit:botstatus").await {
            Ok(val) => Some(val),
            Err(error) => {
                tracing::warn!(?error, "failed to fetch bot status from redis");
                None
            }
        };

        let presence = UpdatePresence {
            op: twilight_model::gateway::OpCode::PresenceUpdate,
            d: discord::gateway::presence(
                if let Some(status) = status {
                    format!("pk;help | {}", status)
                } else {
                    "pk;help".to_string()
                }
                .as_str(),
                false,
            ),
        };

        for sender in senders.iter() {
            match sender.1.command(&presence) {
                Err(error) => {
                    warn!(?error, "could not update presence on shard {}", sender.0)
                }
                _ => {}
            };
        }
    }
}
