use twilight_gateway::Event;
use redis::AsyncCommands;
use prost::Message;

include!(concat!(env!("OUT_DIR"), "/myriad.cache.rs"));

pub async fn handle_event<'a>(
    event: Event,
    rconn: redis::Client
) -> anyhow::Result<()> {
    let mut conn = rconn.get_async_connection().await.unwrap();

    match event {
        // todo: save private channels to sql (see SaveDMChannelStub / PrivateChannelService)
        // todo: save user profiles for some reason (from message create, etc)
        // todo(dotnet): remove relying on cache.OwnUserId
        // todo(dotnet): correctly calculate permissions in guild threads

        Event::GuildCreate(guild) => {
            // todo: clear any existing guild state
            // save guild itself
            conn.hset("discord:guilds", guild.id.get(), CachedGuild{
                id: guild.id.get(),
                name: guild.name.to_string(),
                owner_id: guild.owner_id.get(),
                premium_tier: guild.premium_tier as i32,
            }.encode_to_vec()).await?;
            // save all roles in guild
            for role in guild.roles.clone().into_iter() {
                conn.hset::<&str, u64, Vec<u8>, i32>("discord:roles", role.id.get(), CachedRole{
                    id: role.id.get(),
                    name: role.name,
                    position: role.position as i32,
                    permissions: role.permissions.bits(),
                    mentionable: role.mentionable,
                }.encode_to_vec()).await?;
                // save guild-role map
                conn.hset::<String, u64, u64, i32>(format!("discord:guild_roles:{}", guild.id.get()), role.id.get(), 1).await?;
            }
            // save all channels in guild
            for channel in guild.channels.clone().into_iter() {
                conn.hset::<&str, u64, Vec<u8>, i32>("discord:channels", channel.id.get(), CachedChannel{
                    id: channel.id.get(),
                    r#type: channel.kind as i32,
                    position: channel.position.unwrap_or(0) as i32,
                    name: channel.name,
                    permission_overwrites: channel.permission_overwrites.unwrap().into_iter().map(|v| Overwrite{
                        id: v.id.get(),
                        r#type: v.kind as i32,
                        allow: v.allow.bits(),
                        deny: v.deny.bits(),
                    }).collect(),
                    guild_id: Some(guild.id.get()),
                    parent_id: channel.parent_id.map(|v| v.get()),
                }.encode_to_vec()).await?;
                // save guild-channel map
                conn.hset::<String, u64, u64, i32>(format!("discord:guild_channels:{}", guild.id.get()), channel.id.get(), 1).await?;
            }

            // save all threads in guild (as channels lol)
            for thread in guild.threads.clone().into_iter() {
                conn.hset::<&str, u64, Vec<u8>, i32>("discord:channels", thread.id.get(), CachedChannel{
                    id: thread.id.get(),
                    r#type: thread.kind as i32,
                    position: thread.position.unwrap_or(0) as i32,
                    name: thread.name,
                    guild_id: Some(guild.id.get()),
                    parent_id: thread.parent_id.map(|v| v.get()),
                    ..Default::default()
                }.encode_to_vec()).await?;
                // save guild-channel map
                conn.hset::<String, u64, u64, i32>(format!("discord:guild_channels:{}", guild.id.get()), thread.id.get(), 1).await?;
            }

            // save self guild member
            conn.hset("discord:guild_members", guild.id.get(), CachedGuildMember{
                roles: guild.members.get(0).unwrap().roles.clone().into_iter().map(|r| r.get()).collect()
            }.encode_to_vec()).await?;
            
            // c# code also saves users in guildCreate.Members, but I'm pretty sure that doesn't have anything now because intents
        }
        Event::GuildUpdate(guild) => {
            // save guild itself
            conn.hset("discord:guilds", guild.id.get(), CachedGuild{
                id: guild.id.get(),
                name: guild.name.to_string(),
                owner_id: guild.owner_id.get(),
                premium_tier: guild.premium_tier as i32,
            }.encode_to_vec()).await?;
        }
        Event::GuildDelete(guild) => {
            // delete guild
            conn.hdel("discord:guilds", guild.id.get()).await?;
            if let Ok(roles) = conn.hkeys::<String, Vec<u64>>(format!("discord:guild_roles:{}", guild.id.get())).await {
                for role in roles.into_iter() {
                    conn.hdel("discord:roles", role).await?;
                }
            }
            conn.del(format!("discord:guild_roles:{}", guild.id.get())).await?;

            // this probably should also delete all channels/roles/etc of the guild
            if let Ok(channel) = conn.hkeys::<String, Vec<u64>>(format!("discord:guild_channels:{}", guild.id.get())).await {
                conn.hdel("discord:channels", channel).await?;
            }
            conn.del(format!("discord:guild_channels:{}", guild.id.get())).await?;
        }
        Event::MemberUpdate(member) => {
            // save self guild member
            conn.hset("discord:guild_members", member.guild_id.get(), CachedGuildMember{
                roles: member.roles.clone().into_iter().map(|r| r.get()).collect()
            }.encode_to_vec()).await?;
        }
        Event::ChannelCreate(channel) => {
            // save channel
            conn.hset::<&str, u64, Vec<u8>, i32>("discord:channels", channel.id.get(), CachedChannel{
                id: channel.id.get(),
                r#type: channel.kind as i32,
                position: channel.position.unwrap_or(0) as i32,
                name: channel.name.as_deref().map(|v| v.to_string()),
                permission_overwrites: channel.permission_overwrites.as_ref().unwrap().into_iter().map(|v| Overwrite{
                    id: v.id.get(),
                    r#type: v.kind as i32,
                    allow: v.allow.bits(),
                    deny: v.deny.bits(),
                }).collect(),
                guild_id: channel.guild_id.map(|v| v.get()),
                parent_id: channel.parent_id.map(|v| v.get()),
            }.encode_to_vec()).await?;

            // update guild-channel map (if this is a guild channel)
            if let Some(guild_id) = channel.guild_id {
                conn.hset::<String, u64, u64, i32>(format!("discord:guild_channels:{}", guild_id.get()), channel.id.get(), 1).await?;
            }
        }
        Event::ChannelUpdate(channel) => {
            conn.hset::<&str, u64, Vec<u8>, i32>("discord:channels", channel.id.get(), CachedChannel{
                id: channel.id.get(),
                r#type: channel.kind as i32,
                position: channel.position.unwrap_or(0) as i32,
                name: channel.name.as_deref().map(|v| v.to_string()),
                permission_overwrites: channel.permission_overwrites.as_ref().unwrap().into_iter().map(|v| Overwrite{
                    id: v.id.get(),
                    r#type: v.kind as i32,
                    allow: v.allow.bits(),
                    deny: v.deny.bits(),
                }).collect(),
                guild_id: channel.guild_id.map(|v| v.get()),
                parent_id: channel.parent_id.map(|v| v.get()),
            }.encode_to_vec()).await?;
        }
        Event::ChannelDelete(channel) => {
            // delete channel
            conn.hdel("discord:channels", channel.id.get()).await?;
            // update guild-channel map
            if let Some(guild_id) = channel.guild_id {
                conn.hdel(format!("discord:guild_channels:{}", guild_id.get()), channel.id.get()).await?;
            }
        }
        Event::RoleCreate(role) => {
            // save role
            conn.hset::<&str, u64, Vec<u8>, i32>("discord:roles", role.role.id.get(), CachedRole{
                id: role.role.id.get(),
                name: role.role.name,
                position: role.role.position as i32,
                permissions: role.role.permissions.bits(),
                mentionable: role.role.mentionable,
            }.encode_to_vec()).await?;
            // update guild-role map
            conn.hset::<String, u64, u64, i32>(format!("discord:guild_roles:{}", role.guild_id.get()), role.role.id.get(), 1).await?;
        }
        Event::RoleUpdate(role) => {
            // save role
            conn.hset::<&str, u64, Vec<u8>, i32>("discord:roles", role.role.id.get(), CachedRole{
                id: role.role.id.get(),
                name: role.role.name,
                position: role.role.position as i32,
                permissions: role.role.permissions.bits(),
                mentionable: role.role.mentionable,
            }.encode_to_vec()).await?;
        }
        Event::RoleDelete(role) => {
            // delete role
            conn.hdel("discord:roles", role.role_id.get()).await?;
            // update guild-role map
            conn.hdel(format!("discord:guild_roles:{}", role.guild_id.get()), role.role_id.get()).await?;
        }
        Event::ThreadCreate(thread) => {
            conn.hset::<&str, u64, Vec<u8>, i32>("discord:channels", thread.id.get(), CachedChannel{
                id: thread.id.get(),
                r#type: thread.kind as i32,
                position: thread.position.unwrap_or(0) as i32,
                name: thread.name.as_deref().map(|v| v.to_string()),
                guild_id: Some(thread.guild_id.unwrap().get()),
                parent_id: thread.parent_id.map(|v| v.get()),
                ..Default::default()
            }.encode_to_vec()).await?;

            // save guild-channel map
            conn.hset::<String, u64, u64, i32>(format!("discord:guild_channels:{}", thread.guild_id.unwrap().get()), thread.id.get(), 1).await?;
            // update guild-channel map
        }
        Event::ThreadUpdate(thread) => {
            conn.hset::<&str, u64, Vec<u8>, i32>("discord:channels", thread.id.get(), CachedChannel{
                id: thread.id.get(),
                r#type: thread.kind as i32,
                position: thread.position.unwrap_or(0) as i32,
                name: thread.name.as_deref().map(|v| v.to_string()),
                guild_id: Some(thread.guild_id.unwrap().get()),
                parent_id: thread.parent_id.map(|v| v.get()),
                ..Default::default()
            }.encode_to_vec()).await?;
        }
        Event::ThreadDelete(thread) => {
            // delete channel
            conn.hdel("discord:channels", thread.id.get()).await?;
            // update guild-channel map
            conn.hdel(format!("discord:guild_channels:{}", thread.guild_id.get()), thread.id.get()).await?;
        }
        Event::ThreadListSync(tls) => {
            // save channels
        }
        Event::MessageCreate(message) => {
            // save last message of channel
        }
        _ => {}
    }

    Ok(())
}