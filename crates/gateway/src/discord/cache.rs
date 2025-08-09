use anyhow::format_err;
use lazy_static::lazy_static;
use serde::Serialize;
use std::{collections::HashMap, sync::Arc};
use tokio::sync::RwLock;
use twilight_cache_inmemory::{
    model::CachedMember,
    permission::{MemberRoles, RootError},
    traits::CacheableChannel,
    InMemoryCache, ResourceType,
};
use twilight_gateway::Event;
use twilight_model::{
    channel::{Channel, ChannelType},
    guild::{Guild, Member, Permissions},
    id::{
        marker::{ChannelMarker, GuildMarker, MessageMarker, UserMarker},
        Id,
    },
};
use twilight_util::permission_calculator::PermissionCalculator;

lazy_static! {
    pub static ref DM_PERMISSIONS: Permissions = Permissions::VIEW_CHANNEL
        | Permissions::SEND_MESSAGES
        | Permissions::READ_MESSAGE_HISTORY
        | Permissions::ADD_REACTIONS
        | Permissions::ATTACH_FILES
        | Permissions::EMBED_LINKS
        | Permissions::USE_EXTERNAL_EMOJIS
        | Permissions::CONNECT
        | Permissions::SPEAK
        | Permissions::USE_VAD;
}

pub fn dm_channel(id: Id<ChannelMarker>) -> Channel {
    Channel {
        id,
        kind: ChannelType::Private,

        application_id: None,
        applied_tags: None,
        available_tags: None,
        bitrate: None,
        default_auto_archive_duration: None,
        default_forum_layout: None,
        default_reaction_emoji: None,
        default_sort_order: None,
        default_thread_rate_limit_per_user: None,
        flags: None,
        guild_id: None,
        icon: None,
        invitable: None,
        last_message_id: None,
        last_pin_timestamp: None,
        managed: None,
        member: None,
        member_count: None,
        message_count: None,
        name: None,
        newly_created: None,
        nsfw: None,
        owner_id: None,
        parent_id: None,
        permission_overwrites: None,
        position: None,
        rate_limit_per_user: None,
        recipients: None,
        rtc_region: None,
        thread_metadata: None,
        topic: None,
        user_limit: None,
        video_quality_mode: None,
    }
}

fn member_to_cached_member(item: Member, id: Id<UserMarker>) -> CachedMember {
    CachedMember {
        avatar: item.avatar,
        communication_disabled_until: item.communication_disabled_until,
        deaf: Some(item.deaf),
        flags: item.flags,
        joined_at: item.joined_at,
        mute: Some(item.mute),
        nick: item.nick,
        premium_since: item.premium_since,
        roles: item.roles,
        pending: false,
        user_id: id,
    }
}

pub fn new() -> DiscordCache {
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

    let client = Arc::new(client_builder.build());

    let cache = Arc::new(
        InMemoryCache::builder()
            .resource_types(
                ResourceType::GUILD
                    | ResourceType::CHANNEL
                    | ResourceType::ROLE
                    | ResourceType::USER_CURRENT
                    | ResourceType::MEMBER_CURRENT,
            )
            .message_cache_size(0)
            .build(),
    );

    DiscordCache(
        cache,
        client,
        RwLock::new(Vec::new()),
        RwLock::new(HashMap::new()),
    )
}

#[derive(Clone, Serialize)]
pub struct CachedMessage {
    id: Id<MessageMarker>,
    referenced_message: Option<Id<MessageMarker>>,
    author_username: String,
}

#[derive(Clone, Serialize)]
pub struct LastMessageCacheEntry {
    pub current: CachedMessage,
    pub previous: Option<CachedMessage>,
}

pub struct DiscordCache(
    pub Arc<InMemoryCache>,
    pub Arc<twilight_http::Client>,
    pub RwLock<Vec<u32>>,
    pub RwLock<HashMap<Id<ChannelMarker>, LastMessageCacheEntry>>,
);

impl DiscordCache {
    pub async fn get_last_message(
        &self,
        channel: Id<ChannelMarker>,
    ) -> Option<LastMessageCacheEntry> {
        self.3.read().await.get(&channel).cloned()
    }

    pub async fn update(&self, event: &twilight_gateway::Event) {
        self.0.update(event);

        match event {
            Event::MessageCreate(m) => match self.3.write().await.entry(m.channel_id) {
                std::collections::hash_map::Entry::Occupied(mut e) => {
                    let cur = e.get();
                    e.insert(LastMessageCacheEntry {
                        current: CachedMessage {
                            id: m.id,
                            referenced_message: m.referenced_message.as_ref().map(|v| v.id),
                            author_username: m.author.name.clone(),
                        },
                        previous: Some(cur.current.clone()),
                    });
                }
                std::collections::hash_map::Entry::Vacant(e) => {
                    e.insert(LastMessageCacheEntry {
                        current: CachedMessage {
                            id: m.id,
                            referenced_message: m.referenced_message.as_ref().map(|v| v.id),
                            author_username: m.author.name.clone(),
                        },
                        previous: None,
                    });
                }
            },
            Event::MessageDelete(m) => {
                self.handle_message_deletion(m.channel_id, vec![m.id]).await;
            }
            Event::MessageDeleteBulk(m) => {
                self.handle_message_deletion(m.channel_id, m.ids.clone())
                    .await;
            }
            _ => {}
        };
    }

    async fn handle_message_deletion(
        &self,
        channel_id: Id<ChannelMarker>,
        mids: Vec<Id<MessageMarker>>,
    ) {
        let mut lm = self.3.write().await;

        let Some(entry) = lm.get(&channel_id) else {
            return;
        };

        let mut entry = entry.clone();

        // if none of the deleted messages are relevant, just return
        if !mids.contains(&entry.current.id)
            && entry
                .previous
                .clone()
                .map(|v| !mids.contains(&v.id))
                .unwrap_or(false)
        {
            return;
        }

        // remove "previous" entry if it was deleted
        if let Some(prev) = entry.previous.clone()
            && mids.contains(&prev.id)
        {
            entry.previous = None;
        }

        // set "current" entry to "previous" if current entry was deleted
        // (if the "previous" entry still exists, it was not deleted)
        if let Some(prev) = entry.previous.clone()
            && mids.contains(&entry.current.id)
        {
            entry.current = prev;
            entry.previous = None;
        }

        // if the current entry was already deleted, but previous wasn't,
        // we would've set current to previous
        // so if current is deleted this means both current and previous have
        // been deleted
        // so just drop the cache entry here
        if mids.contains(&entry.current.id) && entry.previous.is_none() {
            lm.remove(&channel_id);
            return;
        }

        // ok, update the entry
        lm.insert(channel_id, entry.clone());
    }

    pub async fn guild_permissions(
        &self,
        guild_id: Id<GuildMarker>,
        user_id: Id<UserMarker>,
    ) -> anyhow::Result<Permissions> {
        if self
            .0
            .guild(guild_id)
            .ok_or_else(|| format_err!("guild not found"))?
            .owner_id()
            == user_id
        {
            return Ok(Permissions::all());
        }

        let member = if user_id
            == libpk::config
                .discord
                .as_ref()
                .expect("missing discord config")
                .client_id
        {
            self.0
                .member(guild_id, user_id)
                .ok_or(format_err!("self member not found"))?
                .value()
                .to_owned()
        } else {
            member_to_cached_member(
                self.1
                    .guild_member(guild_id, user_id)
                    .await?
                    .model()
                    .await?,
                user_id,
            )
        };

        let MemberRoles { assigned, everyone } = self
            .0
            .permissions()
            .member_roles(guild_id, &member)
            .map_err(RootError::from_member_roles)?;
        let calculator =
            PermissionCalculator::new(guild_id, user_id, everyone, assigned.as_slice());

        let permissions = calculator.root();

        Ok(self
            .0
            .permissions()
            .disable_member_communication(&member, permissions))
    }

    pub async fn channel_permissions(
        &self,
        channel_id: Id<ChannelMarker>,
        user_id: Id<UserMarker>,
    ) -> anyhow::Result<Permissions> {
        let channel = self
            .0
            .channel(channel_id)
            .ok_or(format_err!("channel not found"))?;

        if channel.value().guild_id.is_none() {
            return Ok(*DM_PERMISSIONS);
        }

        let guild_id = channel.value().guild_id.unwrap();

        if self
            .0
            .guild(guild_id)
            .ok_or_else(|| {
                tracing::error!(
                    channel_id = channel_id.get(),
                    guild_id = guild_id.get(),
                    "referenced guild from cached channel {channel_id} not found in cache"
                );
                format_err!("internal cache error")
            })?
            .owner_id()
            == user_id
        {
            return Ok(Permissions::all());
        }

        let member = if user_id
            == libpk::config
                .discord
                .as_ref()
                .expect("missing discord config")
                .client_id
        {
            self.0
                .member(guild_id, user_id)
                .ok_or_else(|| {
                    tracing::error!(
                        guild_id = guild_id.get(),
                        "self member for cached guild {guild_id} not found in cache"
                    );
                    format_err!("internal cache error")
                })?
                .value()
                .to_owned()
        } else {
            member_to_cached_member(
                self.1
                    .guild_member(guild_id, user_id)
                    .await?
                    .model()
                    .await?,
                user_id,
            )
        };

        let MemberRoles { assigned, everyone } = self
            .0
            .permissions()
            .member_roles(guild_id, &member)
            .map_err(RootError::from_member_roles)?;

        let overwrites = match channel.kind {
            ChannelType::AnnouncementThread
            | ChannelType::PrivateThread
            | ChannelType::PublicThread => self.0.permissions().parent_overwrites(&channel)?,
            _ => channel
                .value()
                .permission_overwrites()
                .unwrap_or_default()
                .to_vec(),
        };

        let calculator =
            PermissionCalculator::new(guild_id, user_id, everyone, assigned.as_slice());

        let permissions = calculator.in_channel(channel.kind(), overwrites.as_slice());

        Ok(self
            .0
            .permissions()
            .disable_member_communication(&member, permissions))
    }

    // from https://github.com/Gelbpunkt/gateway-proxy/blob/5bcb080a1fcb09f6fafecad7736819663a625d84/src/cache.rs
    pub fn guild(&self, id: Id<GuildMarker>) -> Option<Guild> {
        self.0.guild(id).map(|guild| {
            let channels = self
                .0
                .guild_channels(id)
                .map(|reference| {
                    reference
                        .iter()
                        .filter_map(|channel_id| {
                            let channel = self.0.channel(*channel_id)?;

                            if channel.kind.is_thread() {
                                None
                            } else {
                                Some(channel.value().clone())
                            }
                        })
                        .collect()
                })
                .unwrap_or_default();

            let roles = self
                .0
                .guild_roles(id)
                .map(|reference| {
                    reference
                        .iter()
                        .filter_map(|role_id| {
                            Some(self.0.role(*role_id)?.value().resource().clone())
                        })
                        .collect()
                })
                .unwrap_or_default();

            Guild {
                afk_channel_id: guild.afk_channel_id(),
                afk_timeout: guild.afk_timeout(),
                application_id: guild.application_id(),
                approximate_member_count: None, // Only present in with_counts HTTP endpoint
                banner: guild.banner().map(ToOwned::to_owned),
                approximate_presence_count: None, // Only present in with_counts HTTP endpoint
                channels,
                default_message_notifications: guild.default_message_notifications(),
                description: guild.description().map(ToString::to_string),
                discovery_splash: guild.discovery_splash().map(ToOwned::to_owned),
                emojis: vec![],
                explicit_content_filter: guild.explicit_content_filter(),
                features: guild.features().cloned().collect(),
                icon: guild.icon().map(ToOwned::to_owned),
                id: guild.id(),
                joined_at: guild.joined_at(),
                large: guild.large(),
                max_members: guild.max_members(),
                max_presences: guild.max_presences(),
                max_video_channel_users: guild.max_video_channel_users(),
                member_count: guild.member_count(),
                members: vec![],
                mfa_level: guild.mfa_level(),
                name: guild.name().to_string(),
                nsfw_level: guild.nsfw_level(),
                owner_id: guild.owner_id(),
                owner: guild.owner(),
                permissions: guild.permissions(),
                public_updates_channel_id: guild.public_updates_channel_id(),
                preferred_locale: guild.preferred_locale().to_string(),
                premium_progress_bar_enabled: guild.premium_progress_bar_enabled(),
                premium_subscription_count: guild.premium_subscription_count(),
                premium_tier: guild.premium_tier(),
                presences: vec![],
                roles,
                rules_channel_id: guild.rules_channel_id(),
                safety_alerts_channel_id: guild.safety_alerts_channel_id(),
                splash: guild.splash().map(ToOwned::to_owned),
                stage_instances: vec![],
                stickers: vec![],
                system_channel_flags: guild.system_channel_flags(),
                system_channel_id: guild.system_channel_id(),
                threads: vec![],
                unavailable: Some(false),
                vanity_url_code: guild.vanity_url_code().map(ToString::to_string),
                verification_level: guild.verification_level(),
                voice_states: vec![],
                widget_channel_id: guild.widget_channel_id(),
                widget_enabled: guild.widget_enabled(),
                guild_scheduled_events: guild.guild_scheduled_events().to_vec(),
                max_stage_video_channel_users: guild.max_stage_video_channel_users(),
            }
        })
    }
}
