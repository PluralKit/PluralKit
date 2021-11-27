using Myriad.Cache;
using Myriad.Rest;
using Myriad.Types;

namespace Myriad.Extensions;

public static class CacheExtensions
{
    public static async Task<Guild> GetGuild(this IDiscordCache cache, ulong guildId)
    {
        if (!(await cache.TryGetGuild(guildId) is Guild guild))
            throw new KeyNotFoundException($"Guild {guildId} not found in cache");
        return guild;
    }

    public static async Task<Channel> GetChannel(this IDiscordCache cache, ulong channelId)
    {
        if (!(await cache.TryGetChannel(channelId) is Channel channel))
            throw new KeyNotFoundException($"Channel {channelId} not found in cache");
        return channel;
    }

    public static async Task<User> GetUser(this IDiscordCache cache, ulong userId)
    {
        if (!(await cache.TryGetUser(userId) is User user))
            throw new KeyNotFoundException($"User {userId} not found in cache");
        return user;
    }

    public static async Task<Role> GetRole(this IDiscordCache cache, ulong roleId)
    {
        if (!(await cache.TryGetRole(roleId) is Role role))
            throw new KeyNotFoundException($"Role {roleId} not found in cache");
        return role;
    }

    public static async ValueTask<User?> GetOrFetchUser(this IDiscordCache cache, DiscordApiClient rest,
                                                        ulong userId)
    {
        if (await cache.TryGetUser(userId) is User cacheUser)
            return cacheUser;

        var restUser = await rest.GetUser(userId);
        if (restUser != null)
            await cache.SaveUser(restUser);
        return restUser;
    }

    public static async ValueTask<Channel?> GetOrFetchChannel(this IDiscordCache cache, DiscordApiClient rest,
                                                              ulong channelId)
    {
        if (await cache.TryGetChannel(channelId) is { } cacheChannel)
            return cacheChannel;

        var restChannel = await rest.GetChannel(channelId);
        if (restChannel != null)
            await cache.SaveChannel(restChannel);
        return restChannel;
    }

    public static async Task<Channel> GetOrCreateDmChannel(this IDiscordCache cache, DiscordApiClient rest,
                                                           ulong recipientId)
    {
        if (await cache.TryGetDmChannel(recipientId) is { } cacheChannel)
            return cacheChannel;

        var restChannel = await rest.CreateDm(recipientId);
        await cache.SaveChannel(restChannel);
        return restChannel;
    }

    public static async Task<Channel> GetRootChannel(this IDiscordCache cache, ulong channelOrThread)
    {
        var channel = await cache.GetChannel(channelOrThread);
        if (!channel.IsThread())
            return channel;

        var parent = await cache.GetChannel(channel.ParentId!.Value);
        return parent;
    }
}