use twilight_gateway::Event;

pub async fn handle_event<'a>(
    event: Event,
    rconn: redis::Client
) -> anyhow::Result<()> {
    match event {
        // todo: SaveDMChannelStub (?)
        // todo: save user profiles for some reason (from message create, etc)
        // todo(dotnet): remove relying on cache.OwnUserId

        Event::GuildCreate(guild) => {
            // save guild itself
            // save all roles in guild
            // save guild-role map
            // save all channels in guild
            // save all threads in guild (as channels lol)
            // save guild-channel map
            // save self guild member
            
            // c# code also saves users in guildCreate.Members, but I'm pretty sure that doesn't have anything now because intents
        }
        Event::GuildUpdate(guild) => {
            // save guild itself
        }
        Event::GuildDelete(guild) => {
            // delete guild
            // this probably should also delete all channels/roles/etc of the guild
        }
        Event::MemberUpdate(member) => {
            // save self guild member
        }
        Event::ChannelCreate(channel) => {
            // save channel
            // update guild-channel map
        }
        Event::ChannelUpdate(channel) => {
            // save channel
        }
        Event::ChannelDelete(channel) => {
            // delete channel
            // update guild-channel map
        }
        Event::RoleCreate(role) => {
            // save role
            // update guild-role map
        }
        Event::RoleUpdate(role) => {
            // save role
        }
        Event::RoleDelete(role) => {
            // delete role
            // update guild-role map
        }
        Event::ThreadCreate(thread) => {
            // save channel
            // update guild-channel map
        }
        Event::ThreadUpdate(thread) => {
            // save thread
        }
        Event::ThreadDelete(thread) => {
            // delete channel
            // update guild-channel map
        }
        Event::ThreadListSync(tls) => {
            // save channels
        }
        _ => {}
    }

    Ok(())
}