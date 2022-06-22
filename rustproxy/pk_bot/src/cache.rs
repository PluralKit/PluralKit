use std::sync::{Arc, RwLock};

use dashmap::mapref::one::Ref;
use dashmap::DashMap;
use smol_str::SmolStr;
use twilight_model::channel::permission_overwrite::PermissionOverwrite;
use twilight_model::channel::{Channel, ChannelType};
use twilight_model::gateway::event::Event;
use twilight_model::gateway::payload::incoming::ThreadListSync;
use twilight_model::guild::{Guild, PartialMember, Permissions, PremiumTier, Role};
use twilight_model::id::marker::{ChannelMarker, GuildMarker, RoleMarker, UserMarker};
use twilight_model::id::Id;
use twilight_util::permission_calculator::PermissionCalculator;

const DM_PERMISSIONS: Permissions = Permissions::VIEW_CHANNEL
    .union(Permissions::SEND_MESSAGES)
    .union(Permissions::READ_MESSAGE_HISTORY)
    .union(Permissions::ADD_REACTIONS)
    .union(Permissions::ATTACH_FILES)
    .union(Permissions::EMBED_LINKS)
    .union(Permissions::USE_EXTERNAL_EMOJIS);

#[derive(Debug, Clone)]
pub struct CachedGuild {
    owner_id: u64,
    _premium_tier: PremiumTier,
}

#[derive(Debug, Clone)]
pub struct CachedChannel {
    _name: Option<SmolStr>, // stores strings 22 characters or less inline, which is a large portion of channel names
    _parent_id: Option<Id<ChannelMarker>>,
    guild_id: Option<Id<GuildMarker>>,
    kind: ChannelType,
    overwrites: Vec<PermissionOverwrite>,
}

#[derive(Debug, Clone)]
pub struct CachedRole {
    permissions: Permissions,
    _mentionable: bool,
}

#[derive(Debug, Clone)]
pub struct CachedBotMember {
    roles: Vec<Id<RoleMarker>>,
}

pub struct DiscordCache {
    bot_user: Arc<RwLock<Option<Id<UserMarker>>>>,
    guilds: DashMap<u64, CachedGuild>,
    channels: DashMap<u64, CachedChannel>,
    roles: DashMap<u64, CachedRole>,
    bot_members: DashMap<u64, CachedBotMember>,
}

impl DiscordCache {
    pub fn new() -> DiscordCache {
        DiscordCache {
            bot_user: Arc::new(RwLock::new(None)),
            guilds: DashMap::new(),
            channels: DashMap::new(),
            roles: DashMap::new(),
            bot_members: DashMap::new(),
        }
    }

    pub fn handle_event(&self, event: &Event) {
        match event {
            Event::Ready(ref r) => {
                let mut bot_user = self.bot_user.write().unwrap();
                *bot_user = Some(r.user.id);
            }
            Event::GuildCreate(ref g) => self.update_guild(g),
            Event::ChannelCreate(ref ch) => self.update_channel(ch),
            Event::ChannelUpdate(ref ch) => self.update_channel(ch),
            Event::ChannelDelete(ref ch) => self.delete_channel(ch.id),
            Event::ThreadCreate(ref ch) => self.update_channel(ch),
            Event::ThreadUpdate(ref ch) => self.update_channel(ch),
            Event::ThreadDelete(ref ch) => self.delete_channel(ch.id),
            Event::ThreadListSync(ref ts) => self.update_threads(ts),
            Event::RoleCreate(ref r) => self.update_role(&r.role),
            Event::RoleUpdate(ref r) => self.update_role(&r.role),
            Event::RoleDelete(ref r) => self.delete_role(r.role_id),
            Event::MemberUpdate(ref member) => {
                let current_user = self.bot_user_id();
                if Some(member.user.id) == current_user {
                    self.update_bot_member(member.guild_id, &member.roles);
                }
            }
            _ => {}
        }
    }

    fn update_guild(&self, guild: &Guild) {
        for channel in &guild.channels {
            self.update_channel(channel);
        }

        for role in &guild.roles {
            self.update_role(role);
        }

        let current_user = self.bot_user_id();
        for member in &guild.members {
            if Some(member.user.id) == current_user {
                self.update_bot_member(member.guild_id, &member.roles);
            }
        }

        self.guilds.insert(
            guild.id.get(),
            CachedGuild {
                owner_id: guild.owner_id.get(),
                _premium_tier: guild.premium_tier,
            },
        );
    }

    fn bot_user_id(&self) -> Option<Id<UserMarker>> {
        *self.bot_user.read().unwrap()
    }

    fn update_channel(&self, channel: &Channel) {
        self.channels.insert(
            channel.id.get(),
            CachedChannel {
                _name: channel.name.as_deref().map(SmolStr::new),
                _parent_id: channel.parent_id,
                guild_id: channel.guild_id,
                kind: channel.kind,
                overwrites: channel.permission_overwrites.clone().unwrap_or_default(),
            },
        );
    }

    fn delete_channel(&self, id: Id<ChannelMarker>) {
        self.channels.remove(&id.get());
    }

    fn update_role(&self, role: &Role) {
        self.roles.insert(
            role.id.get(),
            CachedRole {
                permissions: role.permissions,
                _mentionable: role.mentionable,
            },
        );
    }

    fn delete_role(&self, id: Id<RoleMarker>) {
        self.roles.remove(&id.get());
    }

    fn update_threads(&self, evt: &ThreadListSync) {
        for thread in &evt.threads {
            self.update_channel(thread);
        }
    }

    fn update_bot_member(&self, guild_id: Id<GuildMarker>, roles: &[Id<RoleMarker>]) {
        self.bot_members.insert(
            guild_id.get(),
            CachedBotMember {
                roles: roles.to_vec(),
            },
        );
    }

    pub fn get_guild(&self, guild_id: Id<GuildMarker>) -> anyhow::Result<Ref<u64, CachedGuild>> {
        self.guilds
            .get(&guild_id.get())
            .ok_or_else(|| anyhow::anyhow!("could not find guild in cache: {}", guild_id))
    }

    pub fn get_channel(
        &self,
        channel_id: Id<ChannelMarker>,
    ) -> anyhow::Result<Ref<u64, CachedChannel>> {
        self.channels
            .get(&channel_id.get())
            .ok_or_else(|| anyhow::anyhow!("could not find channel in cache: {}", channel_id))
    }

    pub fn get_role(&self, role_id: Id<RoleMarker>) -> anyhow::Result<Ref<u64, CachedRole>> {
        self.roles
            .get(&role_id.get())
            .ok_or_else(|| anyhow::anyhow!("could not find role in cache: {}", role_id))
    }

    pub fn get_bot_member(
        &self,
        guild_id: Id<GuildMarker>,
    ) -> anyhow::Result<Ref<u64, CachedBotMember>> {
        self.bot_members.get(&guild_id.get()).ok_or_else(|| {
            anyhow::anyhow!("could not find bot member in cache for guild: {}", guild_id)
        })
    }

    fn calculate_permissions_in(
        &self,
        channel_id: Id<ChannelMarker>,
        user_id: Id<UserMarker>,
        roles: &[Id<RoleMarker>],
    ) -> anyhow::Result<Permissions> {
        let channel = self.get_channel(channel_id)?;

        if let Some(guild_id) = channel.guild_id {
            let guild = self.get_guild(guild_id)?;
            let everyone_role = self.get_role(guild_id.cast())?;

            let mut member_roles = Vec::with_capacity(roles.len());
            for role_id in roles {
                let role = self.get_role(*role_id)?;
                member_roles.push((role_id.cast(), role.permissions));
            }

            let calc = PermissionCalculator::new(
                guild_id,
                user_id,
                everyone_role.permissions,
                &member_roles,
            )
            .owner_id(Id::new(guild.owner_id));

            Ok(calc.in_channel(channel.kind, &channel.overwrites))
        } else {
            Ok(DM_PERMISSIONS)
        }
    }

    pub fn member_permissions(
        &self,
        channel_id: Id<ChannelMarker>,
        user_id: Id<UserMarker>,
        member: Option<&PartialMember>,
    ) -> anyhow::Result<Permissions> {
        if let Some(member) = member {
            self.calculate_permissions_in(channel_id, user_id, &member.roles)
        } else {
            // this should just be dm perms, probably?
            self.calculate_permissions_in(channel_id, user_id, &[])
        }
    }

    pub fn bot_permissions(&self, channel_id: Id<ChannelMarker>) -> anyhow::Result<Permissions> {
        let channel = self.get_channel(channel_id)?;
        if let Some(guild_id) = channel.guild_id {
            let member = self.get_bot_member(guild_id)?;

            let user_id = self
                .bot_user_id()
                .ok_or_else(|| anyhow::anyhow!("haven't received bot user id yet"))?;

            self.calculate_permissions_in(channel_id, user_id, &member.roles)
        } else {
            Ok(DM_PERMISSIONS)
        }
    }

    pub fn channel_type(&self, channel_id: Id<ChannelMarker>) -> anyhow::Result<ChannelType> {
        let channel = self.get_channel(channel_id)?;
        Ok(channel.kind)
    }
}
