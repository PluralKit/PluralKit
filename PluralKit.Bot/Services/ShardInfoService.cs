using System.Net.WebSockets;

using Myriad.Gateway;

using NodaTime;

using Newtonsoft.Json;

using StackExchange.Redis;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot;

public struct ShardState
{
    [JsonProperty("shard_id")] public int ShardId;
    [JsonProperty("up")] public bool Up;
    [JsonProperty("disconnection_count")] public int DisconnectionCount;
    [JsonProperty("latency")] public int Latency;
    [JsonProperty("last_heartbeat")] public int LastHeartbeat;
    [JsonProperty("last_connection")] public int LastConnection;
    [JsonProperty("cluster_id")] public int? ClusterId;
}

public class ShardInfoService
{
    private readonly int? _clusterId;
    private readonly ILogger _logger;
    private readonly Cluster _client;
    private readonly RedisService _redis;
    private readonly Dictionary<int, ShardInfo> _shardInfo = new();

    public ShardInfoService(ILogger logger, Cluster client, RedisService redis, BotConfig config)
    {
        _logger = logger.ForContext<ShardInfoService>();
        _client = client;
        _redis = redis;
        _clusterId = config.Cluster?.NodeIndex;
    }

    public void Init()
    {
        // We initialize this before any shards are actually created and connected
        // This means the client won't know the shard count, so we attach a listener every time a shard gets connected
        _client.ShardCreated += InitializeShard;
    }

    public async Task<IEnumerable<ShardState>> GetShards()
    {
        if (_redis.Connection == null)
            return new ShardState[] { };
        var db = _redis.Connection.GetDatabase();
        var redisInfo = await db.HashGetAllAsync("pluralkit:shardstatus");
        return redisInfo.Select(x => JsonConvert.DeserializeObject<ShardState>(x.Value));
    }

    private void InitializeShard(Shard shard)
    {
        _ = Inner();

        async Task Inner()
        {
            var db = _redis.Connection.GetDatabase();

            // latency = 1 because otherwise shard 0 would serialize to an empty array, thanks protobuf
            var state = new ShardState() { ShardId = shard.ShardId, Up = false, Latency = 1 };
            if (_clusterId != null)
                state.ClusterId = _clusterId.Value;

            // Register listeners for new shard
            shard.Resumed += () => ReadyOrResumed(shard);
            shard.Ready += () => ReadyOrResumed(shard);
            shard.SocketClosed += (closeStatus, message) => SocketClosed(shard, closeStatus, message);
            shard.HeartbeatReceived += latency => Heartbeated(shard, latency);

            // Register that we've seen it
            await db.HashSetAsync("pluralkit:shardstatus", state.HashWrapper());
        }
    }

    private async Task<ShardState> TryGetShard(Shard shard)
    {
        var db = _redis.Connection.GetDatabase();
        var redisInfo = await db.HashGetAsync("pluralkit:shardstatus", shard.ShardId);
        if (redisInfo.HasValue)
            return JsonConvert.DeserializeObject<ShardState>(redisInfo);
        throw new Exception("expected shard, but didn't get one");
    }

    private void ReadyOrResumed(Shard shard)
    {
        _ = DoAsync(async () =>
        {
            var info = await TryGetShard(shard);

            info.LastConnection = (int)SystemClock.Instance.GetCurrentInstant().ToUnixTimeSeconds();
            info.Up = true;

            var db = _redis.Connection.GetDatabase();
            await db.HashSetAsync("pluralkit:shardstatus", info.HashWrapper());
        });
    }

    private void SocketClosed(Shard shard, WebSocketCloseStatus? closeStatus, string message)
    {
        _ = DoAsync(async () =>
        {
            var info = await TryGetShard(shard);

            info.DisconnectionCount++;
            info.Up = false;

            var db = _redis.Connection.GetDatabase();
            await db.HashSetAsync("pluralkit:shardstatus", info.HashWrapper());
        });
    }

    private void Heartbeated(Shard shard, TimeSpan latency)
    {
        _ = DoAsync(async () =>
        {
            var info = await TryGetShard(shard);

            info.LastHeartbeat = (int)SystemClock.Instance.GetCurrentInstant().ToUnixTimeSeconds();
            info.Up = true;
            info.Latency = (int)latency.TotalMilliseconds;

            var db = _redis.Connection.GetDatabase();
            await db.HashSetAsync("pluralkit:shardstatus", info.HashWrapper());
        });
    }

    private async Task DoAsync(Func<Task> fn)
    {
        // wrapper function to log errors because we "async void" it at call site :(
        try
        {
            await fn();
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error persisting shard status");
        }
    }
}

public static class RedisExt
{
    // convenience method
    public static HashEntry[] HashWrapper(this ShardState state)
        => new[] { new HashEntry(state.ShardId, JsonConvert.SerializeObject(state)) };
}