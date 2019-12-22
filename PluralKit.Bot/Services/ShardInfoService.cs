using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Discord.WebSocket;

using NodaTime;

namespace PluralKit.Bot
{
    public class ShardInfoService
    {
        public class ShardInfo
        {
            public Instant LastConnectionTime;
            public int DisconnectionCount;
            public int ShardLatency;
        }

        private Dictionary<int, ShardInfo> _shardInfo = new Dictionary<int, ShardInfo>();
        
        public void Init(DiscordShardedClient client)
        {
            for (var i = 0; i < client.Shards.Count; i++) 
                _shardInfo[i] = new ShardInfo();

            client.ShardConnected += ShardConnected;
            client.ShardDisconnected += ShardDisconnected;
            client.ShardReady += ShardReady;
            client.ShardLatencyUpdated += ShardLatencyUpdated;
        }

        public ShardInfo GetShardInfo(DiscordSocketClient shard) => _shardInfo[shard.ShardId];

        private Task ShardLatencyUpdated(int oldLatency, int newLatency, DiscordSocketClient shard)
        {
            _shardInfo[shard.ShardId].ShardLatency = newLatency;
            return Task.CompletedTask;
        }

        private Task ShardReady(DiscordSocketClient shard)
        {
            return Task.CompletedTask;
        }

        private Task ShardDisconnected(Exception e, DiscordSocketClient shard)
        {
            _shardInfo[shard.ShardId].DisconnectionCount++;
            return Task.CompletedTask;
        }

        private Task ShardConnected(DiscordSocketClient shard)
        {
            _shardInfo[shard.ShardId].LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            return Task.CompletedTask;
        }
    }
}