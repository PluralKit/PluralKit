using System.Collections.Generic;

using Myriad.Cache;
using Myriad.Types;

namespace Myriad.Extensions
{
    public static class CacheExtensions
    {
        public static Guild GetGuild(this IDiscordCache cache, ulong guildId)
        {
            if (!cache.TryGetGuild(guildId, out var guild))
                throw new KeyNotFoundException($"Guild {guildId} not found in cache");
            return guild;
        }
        
        public static Channel GetChannel(this IDiscordCache cache, ulong channelId)
        {
            if (!cache.TryGetChannel(channelId, out var channel))
                throw new KeyNotFoundException($"Channel {channelId} not found in cache");
            return channel;
        }

        public static Channel? GetChannelOrNull(this IDiscordCache cache, ulong channelId)
        {
            if (cache.TryGetChannel(channelId, out var channel))
                return channel;
            return null;
        }

        public static User GetUser(this IDiscordCache cache, ulong userId)
        {
            if (!cache.TryGetUser(userId, out var user))
                throw new KeyNotFoundException($"User {userId} not found in cache");
            return user;
        }
        
        public static Role GetRole(this IDiscordCache cache, ulong roleId)
        {
            if (!cache.TryGetRole(roleId, out var role))
                throw new KeyNotFoundException($"User {roleId} not found in cache");
            return role;
        }
    }
}