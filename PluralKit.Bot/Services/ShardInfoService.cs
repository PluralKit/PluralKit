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
            _client.SocketOpened += (_, __) => RefreshShardList();
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
                await SocketOpened(shard, null);
                shard.SocketOpened += SocketOpened;
                
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

        private Task SocketOpened(DiscordClient shard, SocketEventArgs _)
        {
            // We do nothing else here, since this kinda doesn't mean *much*? It's only really started once we get Ready/Resumed
            // And it doesn't get fired first time around since we don't have time to add the event listener before it's fired'
            _logger.Information("Shard #{Shard} opened socket", shard.ShardId);
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

        private Task Resumed(DiscordClient shard, ReadyEventArgs e)
        {
            _logger.Information("Shard #{Shard} resumed connection", shard.ShardId);
            
            var info = TryGetShard(shard);
            // info.LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            ReportShardStatus();
            return Task.CompletedTask;
        }

        private Task Ready(DiscordClient shard, ReadyEventArgs e)
        {
            _logger.Information("Shard #{Shard} sent Ready event", shard.ShardId);
            
            var info = TryGetShard(shard);
            info.LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            ReportShardStatus();
            return Task.CompletedTask;
        }

        private Task SocketClosed(DiscordClient shard, SocketCloseEventArgs e)
        {
            _logger.Warning("Shard #{Shard} disconnected ({CloseCode}: {CloseMessage})", shard.ShardId, e.CloseCode, e.CloseMessage);
            
            var info = TryGetShard(shard);
            info.DisconnectionCount++;
            info.Connected = false;
            ReportShardStatus();
            return Task.CompletedTask; 
        }

        private Task Heartbeated(DiscordClient shard, HeartbeatEventArgs e)
        {
            var latency = Duration.FromMilliseconds(e.Ping);
            _logger.Information("Shard #{Shard} received heartbeat (latency: {Latency} ms)", shard.ShardId, latency.Milliseconds);

            var info = TryGetShard(shard);
            info.LastHeartbeatTime = e.Timestamp.ToInstant();
            info.Connected = true;
            info.ShardLatency = latency;
            return Task.CompletedTask;
        }

        public ShardInfo GetShardInfo(DiscordClient shard) => _shardInfo[shard.ShardId];

        public ICollection<ShardInfo> Shards => _shardInfo.Values;
    }
}