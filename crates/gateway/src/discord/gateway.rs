use anyhow::anyhow;
use futures::StreamExt;
use libpk::{_config::ClusterSettings, runtime_config::RuntimeConfig, state::ShardStateEvent};
use metrics::counter;
use std::sync::Arc;
use tokio::sync::mpsc::Sender;
use tracing::{error, info, warn};
use twilight_gateway::{
    create_iterator, ConfigBuilder, Event, EventTypeFlags, Message, Shard, ShardId,
};
use twilight_model::gateway::{
    payload::outgoing::update_presence::UpdatePresencePayload,
    presence::{Activity, ActivityType, Status},
    Intents,
};

use crate::{
    discord::identify_queue::{self, RedisQueue},
    RUNTIME_CONFIG_KEY_EVENT_TARGET,
};

use super::cache::DiscordCache;

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
        if cluster_settings.node_id != 0 {
            return Err(anyhow!(
                "expecting to be node 0 in single-process mode, but we are node {}",
                cluster_settings.node_id
            ));
        }
        (0, (cluster_settings.total_shards - 1).into())
    } else {
        (
            (cluster_settings.node_id * 16).into(),
            (((cluster_settings.node_id + 1) * 16) - 1).into(),
        )
    };

    let prefix = libpk::config
        .discord
        .as_ref()
        .expect("missing discord config")
        .bot_prefix_for_gateway
        .clone();

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
        .presence(presence(format!("{prefix}help").as_str(), false))
        .queue(queue.clone())
        .build(),
        |_, builder| builder.build(),
    );

    let mut shards_vec = Vec::new();
    shards_vec.extend(shards);

    Ok(shards_vec)
}

#[tracing::instrument(fields(shard = %shard.id()), skip_all)]
pub async fn runner(
    mut shard: Shard<RedisQueue>,
    tx: Sender<(ShardId, Event, String)>,
    tx_state: Sender<(ShardId, ShardStateEvent, Option<Event>, Option<i32>)>,
    cache: Arc<DiscordCache>,
    runtime_config: Arc<RuntimeConfig>,
) {
    // let _span = info_span!("shard_runner", shard_id = shard.id().number()).entered();
    let shard_id = shard.id().number();

    let our_user_id = libpk::config
        .discord
        .as_ref()
        .expect("missing discord config")
        .client_id;

    info!("waiting for events");
    while let Some(item) = shard.next().await {
        let raw_event = match item {
            Ok(evt) => match evt {
                Message::Close(frame) => {
                    let close_code = if let Some(close) = frame {
                        close.code.to_string()
                    } else {
                        "unknown".to_string()
                    };

                    info!("shard {shard_id} closed: {close_code}");

                    counter!(
                        "pluralkit_gateway_shard_closed",
                        "shard_id" => shard_id.to_string(),
                        "close_code" => close_code,
                    )
                    .increment(1);

                    if let Err(error) =
                        tx_state.try_send((shard.id(), ShardStateEvent::Closed, None, None))
                    {
                        error!("failed to update shard state for socket closure: {error}");
                    }

                    continue;
                }
                Message::Text(text) => text,
            },
            Err(error) => {
                tracing::warn!(?error, "error receiving event from shard {shard_id}");
                continue;
            }
        };

        let event = match twilight_gateway::parse(raw_event.clone(), EventTypeFlags::all()) {
            Ok(Some(parsed)) => Event::from(parsed),
            Ok(None) => {
                // we received an event type unknown to twilight
                // that's fine, we probably don't need it anyway
                continue;
            }
            Err(error) => {
                error!(?error, ?shard_id, "failed to parse gateway event");
                continue;
            }
        };

        // log the event in metrics
        // event_type * shard_id is too many labels and prometheus fails to query it
        // so we split it into two metrics
        counter!(
            "pluralkit_gateway_events_type",
            "event_type" => serde_variant::to_variant_name(&event.kind()).unwrap(),
        )
        .increment(1);
        counter!(
            "pluralkit_gateway_events_shard",
            "shard_id" => shard_id.to_string(),
        )
        .increment(1);

        // update shard state and discord cache
        if matches!(event, Event::Ready(_)) || matches!(event, Event::Resumed) {
            if let Err(error) = tx_state.try_send((
                shard.id(),
                ShardStateEvent::Other,
                Some(event.clone()),
                None,
            )) {
                tracing::error!(?error, "error updating shard state");
            }
        }
        // need to do heartbeat separately, to get the latency
        let latency_num = shard
            .latency()
            .recent()
            .first()
            .map_or_else(|| 0, |d| d.as_millis()) as i32;
        if let Event::GatewayHeartbeatAck = event
            && let Err(error) = tx_state.try_send((
                shard.id(),
                ShardStateEvent::Heartbeat,
                Some(event.clone()),
                Some(latency_num),
            ))
        {
            tracing::error!(?error, "error updating shard state for latency");
        }

        if let Event::Ready(_) = event {
            if !cache.2.read().await.contains(&shard_id) {
                cache.2.write().await.push(shard_id);
            }
        }
        cache.update(&event).await;

        // okay, we've handled the event internally, let's send it to consumers

        // some basic filtering here is useful
        // we can't use if matching using the | operator, so anything matched does nothing
        // and the default match skips the next block (continues to the next event)
        match event {
            Event::InteractionCreate(_) => {}
            Event::MessageCreate(ref m) if m.author.id != our_user_id => {}
            Event::MessageUpdate(ref m) if m.author.id != our_user_id && !m.author.bot => {}
            Event::MessageDelete(_) => {}
            Event::MessageDeleteBulk(_) => {}
            Event::ReactionAdd(ref r) if r.user_id != our_user_id => {}
            _ => {
                continue;
            }
        }

        if runtime_config.exists(RUNTIME_CONFIG_KEY_EVENT_TARGET).await {
            if let Err(error) = tx.try_send((shard.id(), event, raw_event)) {
                tracing::error!(?error, "error sending shard event");
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
