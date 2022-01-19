using Serilog;

using StackExchange.Redis;

namespace Myriad.Gateway.Limit;

public class RedisRatelimiter: IGatewayRatelimiter
{
    private readonly ILogger _logger;
    private readonly ConnectionMultiplexer _redis;

    private int _concurrency { get; init; }

    // todo: these might need to be tweaked a little
    private static TimeSpan expiry = TimeSpan.FromSeconds(6);
    private static TimeSpan retryInterval = TimeSpan.FromMilliseconds(500);

    public RedisRatelimiter(ILogger logger, ConnectionMultiplexer redis, int concurrency)
    {
        _logger = logger.ForContext<TwilightGatewayRatelimiter>();
        _redis = redis;
        _concurrency = concurrency;
    }

    public async Task Identify(int shard)
    {
        _logger.Information("Shard {ShardId}: requesting identify from Redis", shard);
        var key = "pluralkit:identify:" + (shard % _concurrency).ToString();
        await AcquireLock(key);
    }

    public async Task AcquireLock(string key)
    {
        var conn = _redis.GetDatabase();

        async Task<bool> TryAcquire()
        {
            _logger.Verbose("Trying to acquire lock on key {key} from Redis...", key);
            await Task.Delay(retryInterval);
            return await conn!.StringSetAsync(key, 0, expiry, When.NotExists);
        }

        var acquired = false;
        while (!acquired) acquired = await TryAcquire();
    }
}