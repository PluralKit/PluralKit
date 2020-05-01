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
            foreach (var i in client.ShardClients.Keys)
            {
                _shardInfo[i] = new ShardInfo();

                var shard = client.ShardClients[i];
                shard.Heartbeated += Heartbeated;
                shard.SocketClosed += SocketClosed;
                shard.SocketOpened += () => SocketOpened(shard);
                shard.Ready += Ready;
                shard.Resumed += Resumed;
            }
        }

        private Task Resumed(ReadyEventArgs e)
        {
            _logger.Information("Shard #{Shard} resumed connection", e.Client.ShardId);
            _shardInfo[e.Client.ShardId].LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            _shardInfo[e.Client.ShardId].Connected = true;
            return Task.CompletedTask;
        }

        private Task Ready(ReadyEventArgs e)
        {
            _logger.Information("Shard #{Shard} sent Ready event", e.Client.ShardId);
            _shardInfo[e.Client.ShardId].LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            _shardInfo[e.Client.ShardId].Connected = true;
            return Task.CompletedTask;
        }

        private Task SocketOpened(DiscordClient shard)
        {
            // TODO: do we need this at all? vs. Ready/Resumed
            _logger.Information("Shard #{Shard} connected", shard.ShardId);
            _shardInfo[shard.ShardId].LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            _shardInfo[shard.ShardId].Connected = true;
            return Task.CompletedTask;
        }

        private Task SocketClosed(SocketCloseEventArgs e)
        {
            _logger.Warning("Shard #{Shard} disconnected ({CloseCode}: {CloseMessage})", e.Client.ShardId, e.CloseCode, e.CloseMessage);
            _shardInfo[e.Client.ShardId].DisconnectionCount++;
            _shardInfo[e.Client.ShardId].Connected = false;
            return Task.CompletedTask; 
        }

        private Task Heartbeated(HeartbeatEventArgs e)
        {
            var latency = Duration.FromMilliseconds(e.Ping);
            _logger.Information("Shard #{Shard} received heartbeat (latency: {Latency} ms)", e.Client.ShardId, latency.Milliseconds);
            _shardInfo[e.Client.ShardId].LastHeartbeatTime = e.Timestamp.ToInstant();
            _shardInfo[e.Client.ShardId].ShardLatency = latency;
            return Task.CompletedTask;
        }

        public ShardInfo GetShardInfo(DiscordClient shard) => _shardInfo[shard.ShardId];

        public ICollection<ShardInfo> Shards => _shardInfo.Values;
    }
}