using System.Threading.Tasks;

using Myriad.Gateway;

namespace Myriad.Cache
{
    public static class DiscordCacheExtensions
    {
        public static ValueTask HandleGatewayEvent(this IDiscordCache cache, IGatewayEvent evt)
        {
            switch (evt)
            {
                case GuildCreateEvent gc:
                    return cache.SaveGuildCreate(gc);
                case GuildUpdateEvent gu:
                    return cache.SaveGuild(gu);
                case GuildDeleteEvent gd:
                    return cache.RemoveGuild(gd.Id);
                case ChannelCreateEvent cc:
                    return cache.SaveChannel(cc);
                case ChannelUpdateEvent cu:
                    return cache.SaveChannel(cu);
                case ChannelDeleteEvent cd:
                    return cache.RemoveChannel(cd.Id);
                case GuildRoleCreateEvent grc:
                    return cache.SaveRole(grc.GuildId, grc.Role);
                case GuildRoleUpdateEvent gru:
                    return cache.SaveRole(gru.GuildId, gru.Role);
                case GuildRoleDeleteEvent grd:
                    return cache.RemoveRole(grd.GuildId, grd.RoleId);
                case MessageReactionAddEvent mra:
                    return cache.TrySaveDmChannelStub(mra.GuildId, mra.ChannelId);
                case MessageCreateEvent mc:
                    return cache.SaveMessageCreate(mc);
                case MessageUpdateEvent mu:
                    return cache.TrySaveDmChannelStub(mu.GuildId.Value, mu.ChannelId);
                case MessageDeleteEvent md:
                    return cache.TrySaveDmChannelStub(md.GuildId, md.ChannelId);
                case MessageDeleteBulkEvent md:
                    return cache.TrySaveDmChannelStub(md.GuildId, md.ChannelId);
                case ThreadCreateEvent tc:
                    return cache.SaveChannel(tc);
                case ThreadUpdateEvent tu:
                    return cache.SaveChannel(tu);
                case ThreadDeleteEvent td:
                    return cache.RemoveChannel(td.Id);
                case ThreadListSyncEvent tls:
                    return cache.SaveThreadListSync(tls);
            }

            return default;
        }

        private static async ValueTask SaveGuildCreate(this IDiscordCache cache, GuildCreateEvent guildCreate)
        {
            await cache.SaveGuild(guildCreate);

            foreach (var channel in guildCreate.Channels)
                // The channel object does not include GuildId for some reason...
                await cache.SaveChannel(channel with { GuildId = guildCreate.Id });

            foreach (var member in guildCreate.Members)
                await cache.SaveUser(member.User);

            foreach (var thread in guildCreate.Threads)
                await cache.SaveChannel(thread);
        }

        private static async ValueTask SaveMessageCreate(this IDiscordCache cache, MessageCreateEvent evt)
        {
            await cache.TrySaveDmChannelStub(evt.GuildId, evt.ChannelId);

            await cache.SaveUser(evt.Author);
            foreach (var mention in evt.Mentions)
                await cache.SaveUser(mention);
        }

        private static ValueTask TrySaveDmChannelStub(this IDiscordCache cache, ulong? guildId, ulong channelId)
        {
            // DM messages don't get Channel Create events first, so we need to save
            // some kind of stub channel object until we get the real one
            return guildId != null ? default : cache.SaveDmChannelStub(channelId);
        }

        private static async ValueTask SaveThreadListSync(this IDiscordCache cache, ThreadListSyncEvent evt)
        {
            foreach (var thread in evt.Threads)
                await cache.SaveChannel(thread);
        }
    }
}