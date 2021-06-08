using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;

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
            
            // Register listeners for new shards
            shard.Resumed += () => Resumed(shard);
            shard.Ready += () => Ready(shard);
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

        private void Resumed(Shard shard)
        {
            var info = TryGetShard(shard);
            info.LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            ReportShardStatus();
        }

        private void Ready(Shard shard)
        {
            var info = TryGetShard(shard);
            info.LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            ReportShardStatus();
        }

        private void SocketClosed(Shard shard, WebSocketCloseStatus? closeStatus, string message)
        {
            var info = TryGetShard(shard);
            info.DisconnectionCount++;
            info.Connected = false;
            ReportShardStatus();
        }

        private void Heartbeated(Shard shard, TimeSpan latency)
        {
            var info = TryGetShard(shard);
            info.LastHeartbeatTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            info.ShardLatency = latency.ToDuration();
        }

        public ShardInfo GetShardInfo(Shard shard) => _shardInfo[shard.ShardId];

        public ICollection<ShardInfo> Shards => _shardInfo.Values;
    }
}