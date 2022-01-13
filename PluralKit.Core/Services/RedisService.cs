using StackExchange.Redis;

namespace PluralKit.Core;

public class RedisService
{
    public ConnectionMultiplexer Connection { get; set; }

    public async Task InitAsync(CoreConfig config)
    {
        if (config.RedisAddr != null)
            Connection = await ConnectionMultiplexer.ConnectAsync(config.RedisAddr);
    }
}