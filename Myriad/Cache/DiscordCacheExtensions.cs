using System.Threading.Tasks;

using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Types;

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
                case MessageCreateEvent mc:
                    return cache.SaveMessageCreate(mc);
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
        }

        private static async ValueTask SaveMessageCreate(this IDiscordCache cache, MessageCreateEvent evt)
        {
            await cache.SaveUser(evt.Author);
            foreach (var mention in evt.Mentions) 
                await cache.SaveUser(mention);
        }

        public static async ValueTask<User?> GetOrFetchUser(this IDiscordCache cache, DiscordApiClient rest, ulong userId)
        {
            if (cache.TryGetUser(userId, out var cacheUser))
                return cacheUser;

            var restUser = await rest.GetUser(userId);
            if (restUser != null)
                await cache.SaveUser(restUser);
            return restUser;
        }
    }
}