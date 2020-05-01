using System.Collections.Generic;
using System.Threading.Tasks;

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
            public Instant LastConnectionTime;
            public Instant LastHeartbeatTime;
            public int DisconnectionCount;
            public Duration ShardLatency;
            public bool Connected;
        }

        private ILogger _logger;
        private Dictionary<int, ShardInfo> _shardInfo = new Dictionary<int, ShardInfo>();
        
        public ShardInfoService(ILogger logger)
        {
            _logger = logger.ForContext<ShardInfoService>();
        }

        public void Init(DiscordShardedClient client)
        {
            foreach (var (shardId, shard) in client.ShardClients)
            {
                _shardInfo[shardId] = new ShardInfo();

                shard.Heartbeated += Heartbeated;
                shard.SocketClosed += SocketClosed;
                shard.Ready += Ready;
                shard.Resumed += Resumed;
                shard.SocketOpened += () => SocketOpened(shard);
            }
        }

        private Task SocketOpened(DiscordClient e)
        {
            // We do nothing else here, since this kinda doesn't mean *much*? It's only really started once we get Ready/Resumed
            // And it doesn't get fired first time around since we don't have time to add the event listener before it's fired'
            _logger.Information("Shard #{Shard} opened socket", e.ShardId);
            return Task.CompletedTask;
        }

        private ShardInfo UpdateShard(DiscordClient shard)
        {
            // If we haven't seen this shard before, add it to the dict!
            if (!_shardInfo.TryGetValue(shard.ShardId, out var info))
                _shardInfo[shard.ShardId] = info = new ShardInfo();
            return info;
        }

        private Task Resumed(ReadyEventArgs e)
        {
            _logger.Information("Shard #{Shard} resumed connection", e.Client.ShardId);
            
            var info = UpdateShard(e.Client);
            // info.LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            return Task.CompletedTask;
        }

        private Task Ready(ReadyEventArgs e)
        {
            _logger.Information("Shard #{Shard} sent Ready event", e.Client.ShardId);
            
            var info = UpdateShard(e.Client);
            info.LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            info.Connected = true;
            return Task.CompletedTask;
        }

        private Task SocketClosed(SocketCloseEventArgs e)
        {
            _logger.Warning("Shard #{Shard} disconnected ({CloseCode}: {CloseMessage})", e.Client.ShardId, e.CloseCode, e.CloseMessage);
            
            var info = UpdateShard(e.Client);
            info.DisconnectionCount++;
            info.Connected = false;
            return Task.CompletedTask; 
        }

        private Task Heartbeated(HeartbeatEventArgs e)
        {
            var latency = Duration.FromMilliseconds(e.Ping);
            _logger.Information("Shard #{Shard} received heartbeat (latency: {Latency} ms)", e.Client.ShardId, latency.Milliseconds);

            var info = UpdateShard(e.Client);
            info.LastHeartbeatTime = e.Timestamp.ToInstant();
            info.Connected = true;
            info.ShardLatency = latency;
            return Task.CompletedTask;
        }

        public ShardInfo GetShardInfo(DiscordClient shard) => _shardInfo[shard.ShardId];

        public ICollection<ShardInfo> Shards => _shardInfo.Values;
    }
}