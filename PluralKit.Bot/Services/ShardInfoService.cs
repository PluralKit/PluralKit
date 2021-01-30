using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

using App.Metrics;

using Myriad.Gateway;

using NodaTime;
using NodaTime.Extensions;

using Serilog;

namespace PluralKit.Bot
{
    // TODO: how much of this do we need now that we have logging in the shard library?
    // A lot could probably be cleaned up...
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
        private readonly Cluster _client;
        private readonly Dictionary<int, ShardInfo> _shardInfo = new();
        
        public ShardInfoService(ILogger logger, Cluster client, IMetrics metrics)
        {
            _client = client;
            _metrics = metrics;
            _logger = logger.ForContext<ShardInfoService>();
        }

        public void Init()
        {
            // We initialize this before any shards are actually created and connected
            // This means the client won't know the shard count, so we attach a listener every time a shard gets connected
            _client.ShardCreated += InitializeShard;
        }

        private void ReportShardStatus()
        {
            foreach (var (id, shard) in _shardInfo)
                _metrics.Measure.Gauge.SetValue(BotMetrics.ShardLatency, new MetricTags("shard", id.ToString()), shard.ShardLatency.TotalMilliseconds);
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
            } else _shardInfo[shard.ShardId] = info = new ShardInfo();
            
            // Call our own SocketOpened listener manually (and then attach the listener properly)
            SocketOpened(shard);
            shard.SocketOpened += () => SocketOpened(shard);
                
            // Register listeners for new shards
            _logger.Information("Attaching listeners to new shard #{Shard}", shard.ShardId);
            shard.Resumed += () => Resumed(shard);
            shard.Ready += () => Ready(shard);
            shard.SocketClosed += (closeStatus, message) => SocketClosed(shard, closeStatus, message);
            shard.HeartbeatReceived += latency => Heartbeated(shard, latency);
                
            // Register that we've seen it
            info.HasAttachedListeners = true;

        }

        private void SocketOpened(Shard shard)
        {
            // We do nothing else here, since this kinda doesn't mean *much*? It's only really started once we get Ready/Resumed
            // And it doesn't get fired first time around since we don't have time to add the event listener before it's fired'
            _logger.Information("Shard #{Shard} opened socket", shard.ShardId);
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

        private void Resumed(Shard shard)
        {
            _logger.Information("Shard #{Shard} resumed connection", shard.ShardId);
            
            var info = TryGetShard(shard);
            // info.LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            ReportShardStatus();
        }

        private void Ready(Shard shard)
        {
            _logger.Information("Shard #{Shard} sent Ready event", shard.ShardId);
            
            var info = TryGetShard(shard);
            info.LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            ReportShardStatus();
        }

        private void SocketClosed(Shard shard, WebSocketCloseStatus closeStatus, string message)
        {
            _logger.Warning("Shard #{Shard} disconnected ({CloseCode}: {CloseMessage})", 
                shard.ShardId, closeStatus, message);
            
            var info = TryGetShard(shard);
            info.DisconnectionCount++;
            info.Connected = false;
            ReportShardStatus();
        }

        private void Heartbeated(Shard shard, TimeSpan latency)
        {
            _logger.Information("Shard #{Shard} received heartbeat (latency: {Latency} ms)",
                shard.ShardId, latency.Milliseconds);

            var info = TryGetShard(shard);
            info.LastHeartbeatTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            info.ShardLatency = latency.ToDuration();
        }

        public ShardInfo GetShardInfo(Shard shard) => _shardInfo[shard.ShardId];

        public ICollection<ShardInfo> Shards => _shardInfo.Values;
    }
}