use libpk::_config::ClusterSettings;
use metrics::counter;
use std::sync::{mpsc::Sender, Arc};
use tracing::{info, warn};
use twilight_gateway::{
    create_iterator, ConfigBuilder, Event, EventTypeFlags, Shard, ShardId, StreamExt,
};
use twilight_model::gateway::{
    payload::outgoing::update_presence::UpdatePresencePayload,
    presence::{Activity, ActivityType, Status},
    Intents,
};

use crate::discord::identify_queue::{self, RedisQueue};

use super::{cache::DiscordCache, shard_state::ShardStateManager};

pub fn cluster_config() -> ClusterSettings {
    libpk::config
        .discord
        .as_ref()
        .expect("missing discord config")
        .cluster
        .clone()
        .unwrap_or(libpk::_config::ClusterSettings {
            node_id: 0,
            total_shards: 1,
            total_nodes: 1,
        })
}

pub fn create_shards(redis: fred::clients::RedisPool) -> anyhow::Result<Vec<Shard<RedisQueue>>> {
    let intents = Intents::GUILDS
        | Intents::DIRECT_MESSAGES
        | Intents::DIRECT_MESSAGE_REACTIONS
        | Intents::GUILD_MESSAGES
        | Intents::GUILD_MESSAGE_REACTIONS
        | Intents::MESSAGE_CONTENT;

    let queue = identify_queue::new(redis);

    let cluster_settings = cluster_config();

    let (start_shard, end_shard): (u32, u32) = if cluster_settings.total_shards < 16 {
        warn!("we have less than 16 shards, assuming single gateway process");
        (0, (cluster_settings.total_shards - 1).into())
    } else {
        (
            (cluster_settings.node_id * 16).into(),
            (((cluster_settings.node_id + 1) * 16) - 1).into(),
        )
    };

    let shards = create_iterator(
        start_shard..end_shard + 1,
        cluster_settings.total_shards,
        ConfigBuilder::new(
            libpk::config
                .discord
                .as_ref()
                .expect("missing discord config")
                .bot_token
                .to_owned(),
            intents,
        )
        .presence(presence("pk;help", false))
        .queue(queue.clone())
        .build(),
        |_, builder| builder.build(),
    );

    let mut shards_vec = Vec::new();
    shards_vec.extend(shards);

    Ok(shards_vec)
}

pub async fn runner(
    mut shard: Shard<RedisQueue>,
    tx: Sender<(ShardId, Event)>,
    shard_state: ShardStateManager,
    cache: Arc<DiscordCache>,
) {
    //let _span = info_span!("shard_runner", shard_id = shard.id().number()).entered();
    info!("waiting for events");
    while let Some(item) = shard.next_event(EventTypeFlags::all()).await {
        match item {
            Ok(event) => {
                // event_type * shard_id is too many labels and prometheus fails to query it
                // so we split it into two metrics
                counter!(
                    "pluralkit_gateway_events_type",
                    "event_type" => serde_variant::to_variant_name(&event.kind()).unwrap(),
                )
                .increment(1);
                counter!(
                    "pluralkit_gateway_events_shard",
                    "shard_id" => shard.id().number().to_string(),
                )
                .increment(1);
                if let Err(error) = shard_state
                    .handle_event(shard.id().number(), event.clone())
                    .await
                {
                    tracing::warn!(?error, "error updating redis state")
                }
                if let Event::Ready(_) = event {
                    if !cache.2.read().await.contains(&shard.id().number()) {
                        cache.2.write().await.push(shard.id().number());
                    }
                }
                cache.0.update(&event);
                //if let Err(error) = tx.send((shard.id(), event)) {
                //    tracing::warn!(?error, "error sending event to global handler: {error}",);
                //}
            }
            Err(error) => {
                tracing::warn!(?error, "error receiving event from shard {}", shard.id());
                continue;
            }
        }
    }
}

pub fn presence(status: &str, going_away: bool) -> UpdatePresencePayload {
    UpdatePresencePayload {
        activities: vec![Activity {
            application_id: None,
            assets: None,
            buttons: vec![],
            created_at: None,
            details: None,
            id: None,
            state: None,
            url: None,
            emoji: None,
            flags: None,
            instance: None,
            kind: ActivityType::Playing,
            name: status.to_string(),
            party: None,
            secrets: None,
            timestamps: None,
        }],
        afk: false,
        since: None,
        status: if going_away {
            Status::Idle
        } else {
            Status::Online
        },
    }
}
