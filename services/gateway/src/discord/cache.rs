use anyhow::format_err;
use lazy_static::lazy_static;
use std::sync::Arc;
use twilight_cache_inmemory::{
    model::CachedMember,
    permission::{MemberRoles, RootError},
    traits::CacheableChannel,
    InMemoryCache, ResourceType,
};
use twilight_model::{
    channel::ChannelType,
    guild::{Member, Permissions},
    id::{
        marker::{ChannelMarker, GuildMarker, UserMarker},
        Id,
    },
};
use twilight_util::permission_calculator::PermissionCalculator;

lazy_static! {
    static ref DM_PERMISSIONS: Permissions = Permissions::VIEW_CHANNEL
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
    let mut client_builder =
        twilight_http::Client::builder().token(libpk::config.discord.bot_token.clone());

    if let Some(base_url) = libpk::config.discord.api_base_url.clone() {
        client_builder = client_builder.proxy(base_url, true);
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

    DiscordCache(cache, client)
}

pub struct DiscordCache(pub Arc<InMemoryCache>, pub Arc<twilight_http::Client>);

impl DiscordCache {
    pub async fn guild_permissions(
        &self,
        user_id: Id<UserMarker>,
        guild_id: Id<GuildMarker>,
    ) -> anyhow::Result<Permissions> {
        if self
            .0
            .guild(guild_id)
            .ok_or(format_err!("guild not found"))?
            .owner_id()
            == user_id
        {
            return Ok(Permissions::all());
        }

        let member = if user_id == libpk::config.discord.client_id {
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
        user_id: Id<UserMarker>,
        channel_id: Id<ChannelMarker>,
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
            .ok_or({
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

        let member = if user_id == libpk::config.discord.client_id {
            self.0
                .member(guild_id, user_id)
                .ok_or({
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
}
