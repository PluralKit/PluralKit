using System.Collections.Concurrent;

using Myriad.Gateway.Limit;
using Myriad.Types;

using Serilog;

using StackExchange.Redis;

namespace Myriad.Gateway;

public class Cluster
{
    private readonly GatewaySettings _gatewaySettings;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<int, Shard> _shards = new();
    private IGatewayRatelimiter? _ratelimiter;

    public GatewayStatusUpdate DiscordPresence { get; set; }

    public Cluster(GatewaySettings gatewaySettings, ILogger logger)
    {
        _gatewaySettings = gatewaySettings;
        _logger = logger.ForContext<Cluster>();
    }

    public Func<Shard, IGatewayEvent, Task>? EventReceived { get; set; }

    public IReadOnlyDictionary<int, Shard> Shards => _shards;
    public event Action<Shard>? ShardCreated;

    public async Task Start(GatewayInfo.Bot info, ConnectionMultiplexer? conn = null)
    {
        await Start(info.Url, 0, info.Shards - 1, info.Shards, info.SessionStartLimit.MaxConcurrency, conn);
    }

    public async Task Start(string url, int shardMin, int shardMax, int shardTotal, int recommendedConcurrency, ConnectionMultiplexer? conn = null)
    {
        _ratelimiter = GetRateLimiter(recommendedConcurrency, conn);

        var shardCount = shardMax - shardMin + 1;
        _logger.Information("Starting {ShardCount} of {ShardTotal} shards (#{ShardMin}-#{ShardMax}) at {Url}",
            shardCount, shardTotal, shardMin, shardMax, url);
        for (var i = shardMin; i <= shardMax; i++)
            CreateAndAddShard(url, new ShardInfo(i, shardTotal));

        await StartShards();
    }

    private async Task StartShards()
    {
        _logger.Information("Connecting shards...");
        foreach (var shard in _shards.Values)
            await shard.Start();
    }

    private void CreateAndAddShard(string url, ShardInfo shardInfo)
    {
        var shard = new Shard(_gatewaySettings, shardInfo, _ratelimiter!, url, _logger, DiscordPresence);
        shard.OnEventReceived += evt => OnShardEventReceived(shard, evt);
        _shards[shardInfo.ShardId] = shard;

        ShardCreated?.Invoke(shard);
    }

    private async Task OnShardEventReceived(Shard shard, IGatewayEvent evt)
    {
        if (EventReceived != null)
            await EventReceived(shard, evt);
    }

    private int GetActualShardConcurrency(int recommendedConcurrency)
    {
        if (_gatewaySettings.MaxShardConcurrency == null)
            return recommendedConcurrency;

        return Math.Min(_gatewaySettings.MaxShardConcurrency.Value, recommendedConcurrency);
    }

    private IGatewayRatelimiter GetRateLimiter(int recommendedConcurrency, ConnectionMultiplexer? conn = null)
    {
        var concurrency = GetActualShardConcurrency(recommendedConcurrency);

        if (_gatewaySettings.UseRedisRatelimiter)
        {
            if (conn != null)
                return new RedisRatelimiter(_logger, conn, concurrency);
            else
                _logger.Warning("Tried to get Redis ratelimiter but connection is null! Continuing with local ratelimiter.");
        }

        if (_gatewaySettings.GatewayQueueUrl != null)
            return new TwilightGatewayRatelimiter(_logger, _gatewaySettings.GatewayQueueUrl);

        return new LocalGatewayRatelimiter(_logger, concurrency);
    }
}