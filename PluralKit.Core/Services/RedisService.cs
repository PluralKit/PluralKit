using StackExchange.Redis;

namespace PluralKit.Core;

public class RedisService
{
    public ConnectionMultiplexer? Connection { get; set; }

    public async Task InitAsync(CoreConfig config)
    {
        if (config.RedisAddr != null)
            Connection = await ConnectionMultiplexer.ConnectAsync(config.RedisAddr);
    }

    private string LastMessageKey(ulong userId, ulong channelId) => $"user_last_message:{userId}:{channelId}";

    public Task SetLastMessage(ulong userId, ulong channelId, ulong mid)
        => Connection.GetDatabase().StringSetAsync(LastMessageKey(userId, channelId), mid, expiry: TimeSpan.FromMinutes(10));
    
    public async Task<ulong?> GetLastMessage(ulong userId, ulong channelId)
    {
        var data = await Connection.GetDatabase().StringGetAsync(LastMessageKey(userId, channelId));
        if (data == RedisValue.Null) return null;

        Console.WriteLine($"hhh {data}");

        if (ulong.TryParse(data, out var mid))
            return mid;

        Console.WriteLine("hwat");

        return null;
    }


    private string LoggerCleanKey(ulong userId, ulong guildId) => $"log_cleanup:{userId}:{guildId}";
    
    public Task SetLogCleanup(ulong userId, ulong guildId)
        => Connection.GetDatabase().StringSetAsync(LoggerCleanKey(userId, guildId), 1, expiry: TimeSpan.FromSeconds(3));

    public Task<bool> HasLogCleanup(ulong userId, ulong guildId)
        => Connection.GetDatabase().KeyExistsAsync(LoggerCleanKey(userId, guildId));
}