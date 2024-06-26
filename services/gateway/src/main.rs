use chrono::Timelike;
use std::{
    sync::{mpsc::channel, Arc},
    time::Duration,
    vec::Vec,
};
use tokio::task::JoinSet;
use tracing::{info, warn};
use twilight_model::gateway::payload::outgoing::UpdatePresence;

//use signal_hook::{consts::SIGINT, iterator::Signals};

mod cache_api;
mod discord;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    libpk::init_logging("gateway")?;
    libpk::init_metrics()?;
    info!("hello world");

    let redis = libpk::db::init_redis().await?;

    let shard_state = discord::shard_state::new(redis.clone());
    let cache = Arc::new(discord::cache::new());

    let shards = discord::gateway::create_shards(redis.clone())?;

    let (tx, _rx) = channel();

    let mut senders = Vec::new();

    let mut set = JoinSet::new();
    for shard in shards {
        senders.push((shard.id(), shard.sender()));
        set.spawn(tokio::spawn(discord::gateway::runner(
            shard,
            tx.clone(),
            shard_state.clone(),
            cache.clone(),
        )));
    }

    //todo
    //    let mut signals = Signals::new(&[SIGINT])?;
    //
    //    tokio::spawn(async move {
    //        for sig in signals.forever() {
    //            // todo: set restarting presence
    //            println!("Received signal {:?}", sig);
    //            set.abort_all();
    //        }
    //    });

    tokio::spawn(async move {
        loop {
            tokio::time::sleep(Duration::from_secs(
                (60 - chrono::offset::Utc::now().second()).into(),
            ))
            .await;
            info!("running per-minute scheduled tasks");

            // todo: fetch presence from redis and only update if it changed

            let presence = UpdatePresence {
                op: twilight_model::gateway::OpCode::PresenceUpdate,
                d: discord::gateway::presence("pk;help", false),
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
    });

    cache_api::run_server(cache).await?;

    Ok(())

    // loop {
    // if let Err(err) = {
    // let (shard_id, event) = rx.recv()?;
    // shard_state
    // .handle_event(shard_id.number(), event.clone())
    // .await?;
    // cache.0.update(&event.clone());
    // Ok::<(), anyhow::Error>(())
    // } {
    // error!("processing event: {:#?}", err);
    // }
    // }
}
