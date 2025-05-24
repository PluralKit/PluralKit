#![feature(let_chains)]
#![feature(if_let_guard)]
#![feature(duration_constructors)]

use chrono::Timelike;
use discord::gateway::cluster_config;
use event_awaiter::EventAwaiter;
use fred::{clients::RedisPool, interfaces::*};
use libpk::{runtime_config::RuntimeConfig, state::ShardStateEvent};
use reqwest::{ClientBuilder, StatusCode};
use std::{sync::Arc, time::Duration, vec::Vec};
use tokio::{
    signal::unix::{signal, SignalKind},
    sync::mpsc::channel,
    task::JoinSet,
};
use tracing::{error, info, warn};
use twilight_gateway::{MessageSender, ShardId};
use twilight_model::gateway::payload::outgoing::UpdatePresence;

mod cache_api;
mod discord;
mod event_awaiter;
mod logger;

const RUNTIME_CONFIG_KEY_EVENT_TARGET: &'static str = "event_target";

#[libpk::main]
async fn main() -> anyhow::Result<()> {
    let redis = libpk::db::init_redis().await?;

    let runtime_config = Arc::new(
        RuntimeConfig::new(
            redis.clone(),
            format!(
                "{}:{}",
                libpk::config.runtime_config_key.as_ref().unwrap(),
                cluster_config().node_id
            ),
        )
        .await?,
    );

    // hacky, but needed for selfhost for now
    if let Some(target) = libpk::config
        .discord
        .as_ref()
        .unwrap()
        .gateway_target
        .clone()
    {
        runtime_config
            .set(RUNTIME_CONFIG_KEY_EVENT_TARGET.to_string(), target)
            .await?;
    }

    let cache = Arc::new(discord::cache::new());
    let awaiter = Arc::new(EventAwaiter::new());
    tokio::spawn({
        let awaiter = awaiter.clone();
        async move { awaiter.cleanup_loop().await }
    });

    let shards = discord::gateway::create_shards(redis.clone())?;

    // arbitrary
    // todo: make sure this doesn't fill up
    let (event_tx, mut event_rx) = channel::<(ShardId, twilight_gateway::Event, String)>(1000);

    // todo: make sure this doesn't fill up
    let (state_tx, mut state_rx) = channel::<(
        ShardId,
        ShardStateEvent,
        Option<twilight_gateway::Event>,
        Option<i32>,
    )>(1000);

    let mut senders = Vec::new();
    let mut signal_senders = Vec::new();

    let mut set = JoinSet::new();
    for shard in shards {
        senders.push((shard.id(), shard.sender()));
        signal_senders.push(shard.sender());
        set.spawn(tokio::spawn(discord::gateway::runner(
            shard,
            event_tx.clone(),
            state_tx.clone(),
            cache.clone(),
            runtime_config.clone(),
        )));
    }

    let shard_state = Arc::new(discord::shard_state::new(redis.clone()));

    set.spawn(tokio::spawn({
        let shard_state = shard_state.clone();

        async move {
            while let Some((shard_id, state_event, parsed_event, latency)) = state_rx.recv().await {
                match state_event {
                    ShardStateEvent::Heartbeat => {
                        if !latency.is_none()
                            && let Err(error) = shard_state
                                .heartbeated(shard_id.number(), latency.unwrap())
                                .await
                        {
                            error!("failed to update shard state for heartbeat: {error}")
                        };
                    }
                    ShardStateEvent::Closed => {
                        if let Err(error) = shard_state.socket_closed(shard_id.number()).await {
                            error!("failed to update shard state for heartbeat: {error}")
                        };
                    }
                    ShardStateEvent::Other => {
                        if let Err(error) = shard_state
                            .handle_event(
                                shard_id.number(),
                                parsed_event.expect("shard state event not provided!"),
                            )
                            .await
                        {
                            error!("failed to update shard state for heartbeat: {error}")
                        };
                    }
                }
            }
        }
    }));

    set.spawn(tokio::spawn({
        let runtime_config = runtime_config.clone();
        let awaiter = awaiter.clone();

        async move {
            let client = Arc::new(
                ClientBuilder::new()
                    .connect_timeout(Duration::from_secs(1))
                    .timeout(Duration::from_secs(1))
                    .build()
                    .expect("error making client"),
            );

            while let Some((shard_id, parsed_event, raw_event)) = event_rx.recv().await {
                let target = if let Some(target) = awaiter.target_for_event(parsed_event).await {
                    info!(target = ?target, "sending event to awaiter");
                    Some(target)
                } else if let Some(target) =
                    runtime_config.get(RUNTIME_CONFIG_KEY_EVENT_TARGET).await
                {
                    Some(target)
                } else {
                    None
                };

                if let Some(target) = target {
                    tokio::spawn({
                        let client = client.clone();
                        async move {
                            match client
                                .post(format!("{target}/{}", shard_id.number()))
                                .body(raw_event)
                                .send()
                                .await
                            {
                                Ok(res) => {
                                    if res.status() != StatusCode::OK {
                                        error!(
                                            status = ?res.status(),
                                            target = ?target,
                                            "got non-200 from bot while sending event",
                                        );
                                    }
                                }
                                Err(error) => {
                                    error!(?error, "failed to request event target");
                                }
                            }
                        }
                    });
                }
            }
        }
    }));

    set.spawn(tokio::spawn(
        async move { scheduled_task(redis, senders).await },
    ));

    set.spawn(tokio::spawn(async move {
        match cache_api::run_server(cache, shard_state, runtime_config, awaiter.clone()).await {
            Err(error) => {
                error!(?error, "failed to serve cache api");
            }
            _ => unreachable!(),
        }
    }));

    set.spawn(tokio::spawn(async move {
        signal(SignalKind::interrupt()).unwrap().recv().await;
        info!("got SIGINT");
    }));

    set.spawn(tokio::spawn(async move {
        signal(SignalKind::terminate()).unwrap().recv().await;
        info!("got SIGTERM");
    }));

    set.join_next().await;

    info!("gateway exiting, have a nice day!");

    let presence = UpdatePresence {
        op: twilight_model::gateway::OpCode::PresenceUpdate,
        d: discord::gateway::presence("Restarting... (please wait)", true),
    };

    for sender in signal_senders.iter() {
        let presence = presence.clone();
        let _ = sender.command(&presence);
    }

    set.abort_all();

    // sleep 500ms to allow everything to clean up properly
    tokio::time::sleep(Duration::from_millis(500)).await;

    Ok(())
}

async fn scheduled_task(redis: RedisPool, senders: Vec<(ShardId, MessageSender)>) {
    let prefix = libpk::config
        .discord
        .as_ref()
        .expect("missing discord config")
        .bot_prefix_for_gateway
        .clone();

    println!("{prefix}");

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
                    format!("{prefix}help | {status}")
                } else {
                    format!("{prefix}help")
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
