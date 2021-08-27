using System.Collections.Generic;
using System.Threading.Tasks;

using Myriad.Cache;
using Myriad.Rest;
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

        public static async ValueTask<User?> GetOrFetchUser(this IDiscordCache cache, DiscordApiClient rest, ulong userId)
        {
            if (cache.TryGetUser(userId, out var cacheUser))
                return cacheUser;

            var restUser = await rest.GetUser(userId);
            if (restUser != null)
                await cache.SaveUser(restUser);
            return restUser;
        }

        public static async ValueTask<Channel?> GetOrFetchChannel(this IDiscordCache cache, DiscordApiClient rest, ulong channelId)
        {
            if (cache.TryGetChannel(channelId, out var cacheChannel))
                return cacheChannel;

            var restChannel = await rest.GetChannel(channelId);
            if (restChannel != null)
                await cache.SaveChannel(restChannel);
            return restChannel;
        }

        public static async Task<Channel> GetOrCreateDmChannel(this IDiscordCache cache, DiscordApiClient rest, ulong recipientId)
        {
            if (cache.TryGetDmChannel(recipientId, out var cacheChannel))
                return cacheChannel;

            var restChannel = await rest.CreateDm(recipientId);
            await cache.SaveChannel(restChannel);
            return restChannel;
        }

        public static Channel GetRootChannel(this IDiscordCache cache, ulong channelOrThread)
        {
            var channel = cache.GetChannel(channelOrThread);
            if (!channel.IsThread())
                return channel;

            var parent = cache.GetChannel(channel.ParentId!.Value);
            return parent;
        }
    }
}