// - reaction: (message_id, user_id)
// - message: (author_id, channel_id, ?options)
// - interaction: (custom_id where not_includes "help-menu")

use std::{
    collections::{hash_map::Entry, HashMap},
    net::{IpAddr, SocketAddr},
    time::Duration,
};

use serde::Deserialize;
use tokio::{sync::RwLock, time::Instant};
use tracing::info;
use twilight_gateway::Event;
use twilight_model::{
    application::interaction::InteractionData,
    id::{
        marker::{ChannelMarker, MessageMarker, UserMarker},
        Id,
    },
};

static DEFAULT_TIMEOUT: Duration = Duration::from_mins(15);

#[derive(Deserialize)]
#[serde(untagged)]
pub enum AwaitEventRequest {
    Reaction {
        message_id: Id<MessageMarker>,
        user_id: Id<UserMarker>,
        target: String,
        timeout: Option<u64>,
    },
    Message {
        channel_id: Id<ChannelMarker>,
        author_id: Id<UserMarker>,
        target: String,
        timeout: Option<u64>,
        options: Option<Vec<String>>,
    },
    Interaction {
        id: String,
        target: String,
        timeout: Option<u64>,
    },
}

pub struct EventAwaiter {
    reactions: RwLock<HashMap<(Id<MessageMarker>, Id<UserMarker>), (Instant, String)>>,
    messages: RwLock<
        HashMap<(Id<ChannelMarker>, Id<UserMarker>), (Instant, String, Option<Vec<String>>)>,
    >,
    interactions: RwLock<HashMap<String, (Instant, String)>>,
}

impl EventAwaiter {
    pub fn new() -> Self {
        let v = Self {
            reactions: RwLock::new(HashMap::new()),
            messages: RwLock::new(HashMap::new()),
            interactions: RwLock::new(HashMap::new()),
        };

        v
    }

    pub async fn cleanup_loop(&self) {
        loop {
            tokio::time::sleep(Duration::from_secs(30)).await;
            info!("running event_awaiter cleanup loop");
            let mut counts = (0, 0, 0);
            let now = Instant::now();
            {
                let mut reactions = self.reactions.write().await;
                for key in reactions.clone().keys() {
                    if let Entry::Occupied(entry) = reactions.entry(key.clone())
                        && entry.get().0 < now
                    {
                        counts.0 += 1;
                        entry.remove();
                    }
                }
            }
            {
                let mut messages = self.messages.write().await;
                for key in messages.clone().keys() {
                    if let Entry::Occupied(entry) = messages.entry(key.clone())
                        && entry.get().0 < now
                    {
                        counts.1 += 1;
                        entry.remove();
                    }
                }
            }
            {
                let mut interactions = self.interactions.write().await;
                for key in interactions.clone().keys() {
                    if let Entry::Occupied(entry) = interactions.entry(key.clone())
                        && entry.get().0 < now
                    {
                        counts.2 += 1;
                        entry.remove();
                    }
                }
            }
            info!("ran event_awaiter cleanup loop, took {}us, {} reactions, {} messages, {} interactions", Instant::now().duration_since(now).as_micros(), counts.0, counts.1, counts.2);
        }
    }

    pub async fn target_for_event(&self, event: Event) -> Option<String> {
        match event {
            Event::MessageCreate(message) => {
                let mut messages = self.messages.write().await;

                messages
                    .remove(&(message.channel_id, message.author.id))
                    .map(|(timeout, target, options)| {
                        if let Some(options) = options
                            && !options.contains(&message.content.to_lowercase())
                        {
                            messages.insert(
                                (message.channel_id, message.author.id),
                                (timeout, target, Some(options)),
                            );
                            return None;
                        }
                        Some((*target).to_string())
                    })?
            }
            Event::ReactionAdd(reaction)
                if let Some((_, target)) = self
                    .reactions
                    .write()
                    .await
                    .remove(&(reaction.message_id, reaction.user_id)) =>
            {
                Some((*target).to_string())
            }
            Event::InteractionCreate(interaction)
                if let Some(data) = interaction.data.clone()
                    && let InteractionData::MessageComponent(component) = data
                    && !component.custom_id.contains("help-menu")
                    && let Some((_, target)) =
                        self.interactions.write().await.remove(&component.custom_id) =>
            {
                Some((*target).to_string())
            }

            _ => None,
        }
    }

    pub async fn handle_request(&self, req: AwaitEventRequest, addr: SocketAddr) {
        match req {
            AwaitEventRequest::Reaction {
                message_id,
                user_id,
                target,
                timeout,
            } => {
                self.reactions.write().await.insert(
                    (message_id, user_id),
                    (
                        Instant::now()
                            .checked_add(
                                timeout
                                    .map(|i| Duration::from_secs(i))
                                    .unwrap_or(DEFAULT_TIMEOUT),
                            )
                            .expect("invalid time"),
                        target_or_addr(target, addr),
                    ),
                );
            }
            AwaitEventRequest::Message {
                channel_id,
                author_id,
                target,
                timeout,
                options,
            } => {
                self.messages.write().await.insert(
                    (channel_id, author_id),
                    (
                        Instant::now()
                            .checked_add(
                                timeout
                                    .map(|i| Duration::from_secs(i))
                                    .unwrap_or(DEFAULT_TIMEOUT),
                            )
                            .expect("invalid time"),
                        target_or_addr(target, addr),
                        options,
                    ),
                );
            }
            AwaitEventRequest::Interaction {
                id,
                target,
                timeout,
            } => {
                self.interactions.write().await.insert(
                    id,
                    (
                        Instant::now()
                            .checked_add(
                                timeout
                                    .map(|i| Duration::from_secs(i))
                                    .unwrap_or(DEFAULT_TIMEOUT),
                            )
                            .expect("invalid time"),
                        target_or_addr(target, addr),
                    ),
                );
            }
        }
    }

    pub async fn clear(&self) {
        self.reactions.write().await.clear();
        self.messages.write().await.clear();
        self.interactions.write().await.clear();
    }
}

fn target_or_addr(target: String, addr: SocketAddr) -> String {
    if target == "source-addr" {
        let ip_str = match addr.ip() {
            IpAddr::V4(v4) => v4.to_string(),
            IpAddr::V6(v6) => {
                if let Some(v4) = v6.to_ipv4_mapped() {
                    v4.to_string()
                } else {
                    format!("[{v6}]")
                }
            }
        };
        format!("http://{ip_str}:5002/events")
    } else {
        target
    }
}
