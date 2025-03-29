using Myriad.Cache;
using Myriad.Rest;
using Myriad.Types;

namespace Myriad.Extensions;

public static class CacheExtensions
{
    public static async Task<Guild> GetGuild(this IDiscordCache cache, ulong guildId)
    {
        if (!(await cache.TryGetGuild(guildId) is Guild guild))
            throw new NotFoundInCacheException(guildId, "guild");
        return guild;
    }

    public static async Task<Channel> GetChannel(this IDiscordCache cache, ulong guildId, ulong channelId)
    {
        if (!(await cache.TryGetChannel(guildId, channelId) is Channel channel))
            throw new NotFoundInCacheException(channelId, "channel");
        return channel;
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
                                                              ulong guildId, ulong channelId)
    {
        if (await cache.TryGetChannel(guildId, channelId) is { } cacheChannel)
            return cacheChannel;

        var restChannel = await rest.GetChannel(channelId);
        if (restChannel != null)
            await cache.SaveChannel(restChannel);
        return restChannel;
    }

    public static async Task<Channel> GetRootChannel(this IDiscordCache cache, ulong guildId, ulong channelOrThread)
    {
        var channel = await cache.GetChannel(guildId, channelOrThread);
        if (!channel.IsThread())
            return channel;

        var parent = await cache.TryGetChannel(guildId, channel.ParentId!.Value);
        if (parent == null) throw new Exception($"failed to find parent channel for thread {channelOrThread} in cache");
        return parent;
    }
}

public class NotFoundInCacheException: Exception
{
    public ulong EntityId { get; init; }
    public string EntityType { get; init; }

    public NotFoundInCacheException(ulong id, string type) : base("expected entity in discord cache but was not found")
    {
        EntityId = id;
        EntityType = type;
    }
}