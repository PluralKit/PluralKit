using StackExchange.Redis;

namespace PluralKit.Core;

public class RedisService
{
    public ConnectionMultiplexer? Connection { get; set; }

    public async Task InitAsync(CoreConfig config)
    {
        Connection = await ConnectionMultiplexer.ConnectAsync(config.RedisAddr);
    }

    private string LastMessageKey(ulong userId, ulong channelId) => $"user_last_message:{userId}:{channelId}";
    public Task SetLastMessage(ulong userId, ulong channelId, ulong mid)
        => Connection.GetDatabase().UlongSetAsync(LastMessageKey(userId, channelId), mid, expiry: TimeSpan.FromMinutes(10));
    public Task<ulong?> GetLastMessage(ulong userId, ulong channelId)
        => Connection.GetDatabase().UlongGetAsync(LastMessageKey(userId, channelId));

    private string LoggerCleanKey(ulong userId, ulong guildId) => $"log_cleanup:{userId}:{guildId}";
    public Task SetLogCleanup(ulong userId, ulong guildId)
        => Connection.GetDatabase().StringSetAsync(LoggerCleanKey(userId, guildId), 1, expiry: TimeSpan.FromSeconds(3));
    public Task<bool> HasLogCleanup(ulong userId, ulong guildId)
        => Connection.GetDatabase().KeyExistsAsync(LoggerCleanKey(userId, guildId));

    // note: these methods are named weird - they actually get the proxied mid from the original mid
    // but anything else would've been more confusing
    private string OriginalMidKey(ulong original_mid) => $"original_mid:{original_mid}";
    public Task SetOriginalMid(ulong original_mid, ulong proxied_mid)
        => Connection.GetDatabase().UlongSetAsync(OriginalMidKey(original_mid), proxied_mid, expiry: TimeSpan.FromMinutes(30));
    public Task<ulong?> GetOriginalMid(ulong original_mid)
        => Connection.GetDatabase().UlongGetAsync(OriginalMidKey(original_mid));
}

public static class RedisExt
{
    public static async Task<ulong?> UlongGetAsync(this StackExchange.Redis.IDatabase database, string key)
    {
        var data = await database.StringGetAsync(key);
        if (data == RedisValue.Null) return null;

        if (ulong.TryParse(data, out var value))
            return value;

        return null;
    }

    public static Task UlongSetAsync(this StackExchange.Redis.IDatabase database, string key, ulong value, TimeSpan? expiry = null)
        => database.StringSetAsync(key, value.ToString(), expiry);
}