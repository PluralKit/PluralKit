using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using App.Metrics;

using DSharpPlus;
using DSharpPlus.EventArgs;

using NodaTime;
using NodaTime.Extensions;

using Serilog;

namespace PluralKit.Bot
{
    public class ShardInfoService
    {
        public class ShardInfo
        {
            public bool HasAttachedListeners;
            public Instant LastConnectionTime;
            public Instant LastHeartbeatTime;
            public int DisconnectionCount;
            public Duration ShardLatency;
            public bool Connected;
        }

        private readonly IMetrics _metrics;
        private readonly ILogger _logger;
        private readonly DiscordShardedClient _client;
        private readonly Dictionary<int, ShardInfo> _shardInfo = new Dictionary<int, ShardInfo>();
        
        public ShardInfoService(ILogger logger, DiscordShardedClient client, IMetrics metrics)
        {
            _client = client;
            _metrics = metrics;
            _logger = logger.ForContext<ShardInfoService>();
        }

        public void Init()
        {
            // We initialize this before any shards are actually created and connected
            // This means the client won't know the shard count, so we attach a listener every time a shard gets connected
            _client.SocketOpened += RefreshShardList;
        }

        private void ReportShardStatus()
        {
            foreach (var (id, shard) in _shardInfo)
                _metrics.Measure.Gauge.SetValue(BotMetrics.ShardLatency, new MetricTags("shard", id.ToString()), shard.ShardLatency.TotalMilliseconds);
            _metrics.Measure.Gauge.SetValue(BotMetrics.ShardsConnected, _shardInfo.Count(s => s.Value.Connected));
        }

        private async Task RefreshShardList()
        {
            // This callback doesn't actually receive the shard that was opening, so we just try to check we have 'em all (so far)
            foreach (var (id, shard) in _client.ShardClients)
            {
                // Get or insert info in the client dict
                if (_shardInfo.TryGetValue(id, out var info))
                {
                    // Skip adding listeners if we've seen this shard & already added listeners to it
                    if (info.HasAttachedListeners) continue;
                } else _shardInfo[id] = info = new ShardInfo();
                
                
                // Call our own SocketOpened listener manually (and then attach the listener properly)
                await SocketOpened(shard);
                shard.SocketOpened += () => SocketOpened(shard);
                
                // Register listeners for new shards
                _logger.Information("Attaching listeners to new shard #{Shard}", shard.ShardId);
                shard.Resumed += Resumed;
                shard.Ready += Ready;
                shard.SocketClosed += SocketClosed;
                shard.Heartbeated += Heartbeated;
                
                // Register that we've seen it
                info.HasAttachedListeners = true;
            }
        }

        private Task SocketOpened(DiscordClient e)
        {
            // We do nothing else here, since this kinda doesn't mean *much*? It's only really started once we get Ready/Resumed
            // And it doesn't get fired first time around since we don't have time to add the event listener before it's fired'
            _logger.Information("Shard #{Shard} opened socket", e.ShardId);
            return Task.CompletedTask;
        }

        private ShardInfo TryGetShard(DiscordClient shard)
        {
            // If we haven't seen this shard before, add it to the dict!
            // I don't think this will ever occur since the shard number is constant up-front and we handle those
            // in the RefreshShardList handler above but you never know, I guess~
            if (!_shardInfo.TryGetValue(shard.ShardId, out var info))
                _shardInfo[shard.ShardId] = info = new ShardInfo();
            return info;
        }

        private Task Resumed(ReadyEventArgs e)
        {
            _logger.Information("Shard #{Shard} resumed connection", e.Client.ShardId);
            
            var info = TryGetShard(e.Client);
            // info.LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            ReportShardStatus();
            return Task.CompletedTask;
        }

        private Task Ready(ReadyEventArgs e)
        {
            _logger.Information("Shard #{Shard} sent Ready event", e.Client.ShardId);
            
            var info = TryGetShard(e.Client);
            info.LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            ReportShardStatus();
            return Task.CompletedTask;
        }

        private Task SocketClosed(SocketCloseEventArgs e)
        {
            _logger.Warning("Shard #{Shard} disconnected ({CloseCode}: {CloseMessage})", e.Client.ShardId, e.CloseCode, e.CloseMessage);
            
            var info = TryGetShard(e.Client);
            info.DisconnectionCount++;
            info.Connected = false;
            ReportShardStatus();
            return Task.CompletedTask; 
        }

        private Task Heartbeated(HeartbeatEventArgs e)
        {
            var latency = Duration.FromMilliseconds(e.Ping);
            _logger.Information("Shard #{Shard} received heartbeat (latency: {Latency} ms)", e.Client.ShardId, latency.Milliseconds);

            var info = TryGetShard(e.Client);
            info.LastHeartbeatTime = e.Timestamp.ToInstant();
            info.Connected = true;
            info.ShardLatency = latency;
            return Task.CompletedTask;
        }

        public ShardInfo GetShardInfo(DiscordClient shard) => _shardInfo[shard.ShardId];

        public ICollection<ShardInfo> Shards => _shardInfo.Values;
    }
}