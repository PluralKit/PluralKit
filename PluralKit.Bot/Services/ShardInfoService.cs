using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using DSharpPlus;

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
            foreach (var i in client.ShardClients.Keys) 
                _shardInfo[i] = new ShardInfo();
            
            // TODO
            // client.ShardConnected += ShardConnected;
            // client.ShardDisconnected += ShardDisconnected;
            // client.ShardReady += ShardReady;
            // client.ShardLatencyUpdated += ShardLatencyUpdated;
        }

        public ShardInfo GetShardInfo(DiscordClient shard) => _shardInfo[shard.ShardId];

        private Task ShardLatencyUpdated(int oldLatency, int newLatency, DiscordClient shard)
        {
            _shardInfo[shard.ShardId].ShardLatency = newLatency;
            return Task.CompletedTask;
        }

        private Task ShardReady(DiscordClient shard)
        {
            return Task.CompletedTask;
        }

        private Task ShardDisconnected(Exception e, DiscordClient shard)
        {
            _shardInfo[shard.ShardId].DisconnectionCount++;
            return Task.CompletedTask;
        }

        private Task ShardConnected(DiscordClient shard)
        {
            _shardInfo[shard.ShardId].LastConnectionTime = SystemClock.Instance.GetCurrentInstant();
            return Task.CompletedTask;
        }
    }
}