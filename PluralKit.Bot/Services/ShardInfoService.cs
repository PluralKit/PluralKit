using System.Net.WebSockets;

using App.Metrics;

using Myriad.Gateway;

using NodaTime;
using NodaTime.Extensions;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot;

// TODO: how much of this do we need now that we have logging in the shard library?
// A lot could probably be cleaned up...
public class ShardInfoService
{
    private readonly Cluster _client;

    private readonly IDatabase _db;
    private readonly ILogger _logger;

    private readonly IMetrics _metrics;
    private readonly ModelRepository _repo;
    private readonly Dictionary<int, ShardInfo> _shardInfo = new();

    public ShardInfoService(ILogger logger, Cluster client, IMetrics metrics, IDatabase db, ModelRepository repo)
    {
        _client = client;
        _metrics = metrics;
        _db = db;
        _repo = repo;
        _logger = logger.ForContext<ShardInfoService>();
    }

    public ICollection<ShardInfo> Shards => _shardInfo.Values;

    public void Init()
    {
        // We initialize this before any shards are actually created and connected
        // This means the client won't know the shard count, so we attach a listener every time a shard gets connected
        _client.ShardCreated += InitializeShard;
    }

    private void ReportShardStatus()
    {
        foreach (var (id, shard) in _shardInfo)
            _metrics.Measure.Gauge.SetValue(BotMetrics.ShardLatency, new MetricTags("shard", id.ToString()),
                shard.ShardLatency.TotalMilliseconds);
        _metrics.Measure.Gauge.SetValue(BotMetrics.ShardsConnected, _shardInfo.Count(s => s.Value.Connected));
    }

    private void InitializeShard(Shard shard)
    {
        // Get or insert info in the client dict
        if (_shardInfo.TryGetValue(shard.ShardId, out var info))
        {
            // Skip adding listeners if we've seen this shard & already added listeners to it
            if (info.HasAttachedListeners)
                return;
        }
        else
        {
            _shardInfo[shard.ShardId] = info = new ShardInfo();
        }

        // Call our own SocketOpened listener manually (and then attach the listener properly)

        // Register listeners for new shards
        shard.Resumed += () => ReadyOrResumed(shard);
        shard.Ready += () => ReadyOrResumed(shard);
        shard.SocketClosed += (closeStatus, message) => SocketClosed(shard, closeStatus, message);
        shard.HeartbeatReceived += latency => Heartbeated(shard, latency);

        // Register that we've seen it
        info.HasAttachedListeners = true;
    }

    private ShardInfo TryGetShard(Shard shard)
    {
        // If we haven't seen this shard before, add it to the dict!
        // I don't think this will ever occur since the shard number is constant up-front and we handle those
        // in the RefreshShardList handler above but you never know, I guess~
        if (!_shardInfo.TryGetValue(shard.ShardId, out var info))
            _shardInfo[shard.ShardId] = info = new ShardInfo();
        return info;
    }

    private void ReadyOrResumed(Shard shard)
    {
        var info = TryGetShard(shard);
        info.LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
        info.Connected = true;
        ReportShardStatus();

        _ = ExecuteWithDatabase(async c =>
        {
            await _repo.SetShardStatus(c, shard.ShardId, PKShardInfo.ShardStatus.Up);
            await _repo.RegisterShardConnection(c, shard.ShardId);
        });
    }

    private void SocketClosed(Shard shard, WebSocketCloseStatus? closeStatus, string message)
    {
        var info = TryGetShard(shard);
        info.DisconnectionCount++;
        info.Connected = false;
        ReportShardStatus();

        _ = ExecuteWithDatabase(c =>
            _repo.SetShardStatus(c, shard.ShardId, PKShardInfo.ShardStatus.Down));
    }

    private void Heartbeated(Shard shard, TimeSpan latency)
    {
        var info = TryGetShard(shard);
        info.LastHeartbeatTime = SystemClock.Instance.GetCurrentInstant();
        info.Connected = true;
        info.ShardLatency = latency.ToDuration();

        _ = ExecuteWithDatabase(c =>
            _repo.RegisterShardHeartbeat(c, shard.ShardId, latency.ToDuration()));
    }

    private async Task ExecuteWithDatabase(Func<IPKConnection, Task> fn)
    {
        // wrapper function to log errors because we "async void" it at call site :(
        try
        {
            await using var conn = await _db.Obtain();
            await fn(conn);
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error persisting shard status");
        }
    }

    public ShardInfo GetShardInfo(Shard shard) => _shardInfo[shard.ShardId];

    public class ShardInfo
    {
        public bool Connected;
        public int DisconnectionCount;
        public bool HasAttachedListeners;
        public Instant LastConnectionTime;
        public Instant LastHeartbeatTime;
        public Duration ShardLatency;
    }
}