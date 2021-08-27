using System;
using System.Collections.Generic;

namespace Myriad.Gateway
{
    public interface IGatewayEvent
    {
        public static readonly Dictionary<string, Type> EventTypes = new()
        {
            { "READY", typeof(ReadyEvent) },
            { "RESUMED", typeof(ResumedEvent) },
            { "GUILD_CREATE", typeof(GuildCreateEvent) },
            { "GUILD_UPDATE", typeof(GuildUpdateEvent) },
            { "GUILD_DELETE", typeof(GuildDeleteEvent) },
            { "GUILD_MEMBER_ADD", typeof(GuildMemberAddEvent) },
            { "GUILD_MEMBER_REMOVE", typeof(GuildMemberRemoveEvent) },
            { "GUILD_MEMBER_UPDATE", typeof(GuildMemberUpdateEvent) },
            { "GUILD_ROLE_CREATE", typeof(GuildRoleCreateEvent) },
            { "GUILD_ROLE_UPDATE", typeof(GuildRoleUpdateEvent) },
            { "GUILD_ROLE_DELETE", typeof(GuildRoleDeleteEvent) },
            { "CHANNEL_CREATE", typeof(ChannelCreateEvent) },
            { "CHANNEL_UPDATE", typeof(ChannelUpdateEvent) },
            { "CHANNEL_DELETE", typeof(ChannelDeleteEvent) },
            { "THREAD_CREATE", typeof(ThreadCreateEvent) },
            { "THREAD_UPDATE", typeof(ThreadUpdateEvent) },
            { "THREAD_DELETE", typeof(ThreadDeleteEvent) },
            { "THREAD_LIST_SYNC", typeof(ThreadListSyncEvent) },
            { "MESSAGE_CREATE", typeof(MessageCreateEvent) },
            { "MESSAGE_UPDATE", typeof(MessageUpdateEvent) },
            { "MESSAGE_DELETE", typeof(MessageDeleteEvent) },
            { "MESSAGE_DELETE_BULK", typeof(MessageDeleteBulkEvent) },
            { "MESSAGE_REACTION_ADD", typeof(MessageReactionAddEvent) },
            { "MESSAGE_REACTION_REMOVE", typeof(MessageReactionRemoveEvent) },
            { "MESSAGE_REACTION_REMOVE_ALL", typeof(MessageReactionRemoveAllEvent) },
            { "MESSAGE_REACTION_REMOVE_EMOJI", typeof(MessageReactionRemoveEmojiEvent) },
            { "INTERACTION_CREATE", typeof(InteractionCreateEvent) }
        };
    }
}