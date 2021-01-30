using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Myriad.Types;

using Serilog;

namespace Myriad.Gateway
{
    public class Cluster
    {
        private readonly GatewaySettings _gatewaySettings;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<int, Shard> _shards = new();

        public Cluster(GatewaySettings gatewaySettings, ILogger logger)
        {
            _gatewaySettings = gatewaySettings;
            _logger = logger;
        }

        public Func<Shard, IGatewayEvent, Task>? EventReceived { get; set; }
        public event Action<Shard>? ShardCreated;

        public IReadOnlyDictionary<int, Shard> Shards => _shards;
        public ClusterSessionState SessionState => GetClusterState();
        public User? User => _shards.Values.Select(s => s.User).FirstOrDefault(s => s != null);
        public ApplicationPartial? Application => _shards.Values.Select(s => s.Application).FirstOrDefault(s => s != null);

        private ClusterSessionState GetClusterState()
        {
            var shards = new List<ClusterSessionState.ShardState>();
            foreach (var (id, shard) in _shards)
                shards.Add(new ClusterSessionState.ShardState
                {
                    Shard = shard.ShardInfo, 
                    Session = shard.SessionInfo
                });

            return new ClusterSessionState {Shards = shards};
        }

        public async Task Start(GatewayInfo.Bot info, ClusterSessionState? lastState = null)
        {
            if (lastState != null && lastState.Shards.Count == info.Shards)
                await Resume(info.Url, lastState);
            else
                await Start(info.Url, info.Shards);
        }

        public async Task Resume(string url, ClusterSessionState sessionState)
        {
            _logger.Information("Resuming session with {ShardCount} shards at {Url}", sessionState.Shards.Count, url);
            foreach (var shardState in sessionState.Shards)
                CreateAndAddShard(url, shardState.Shard, shardState.Session);

            await StartShards();
        }

        public async Task Start(string url, int shardCount)
        {
            _logger.Information("Starting {ShardCount} shards at {Url}", shardCount, url);
            for (var i = 0; i < shardCount; i++)
                CreateAndAddShard(url, new ShardInfo(i, shardCount), null);

            await StartShards();
        }

        private async Task StartShards()
        {
            _logger.Information("Connecting shards...");
            await Task.WhenAll(_shards.Values.Select(s => s.Start()));
        }

        private void CreateAndAddShard(string url, ShardInfo shardInfo, ShardSessionInfo? session)
        {
            var shard = new Shard(_logger, new Uri(url), _gatewaySettings, shardInfo, session);
            shard.OnEventReceived += evt => OnShardEventReceived(shard, evt);
            _shards[shardInfo.ShardId] = shard;
            
            ShardCreated?.Invoke(shard);
        }

        private async Task OnShardEventReceived(Shard shard, IGatewayEvent evt)
        {
            if (EventReceived != null)
                await EventReceived(shard, evt);
        }
    }
}