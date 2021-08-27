using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Myriad.Gateway.Limit;
using Myriad.Types;

using Serilog;

namespace Myriad.Gateway
{
    public class Cluster
    {
        private readonly GatewaySettings _gatewaySettings;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<int, Shard> _shards = new();
        private IGatewayRatelimiter? _ratelimiter;

        public Cluster(GatewaySettings gatewaySettings, ILogger logger)
        {
            _gatewaySettings = gatewaySettings;
            _logger = logger.ForContext<Cluster>();
        }

        public Func<Shard, IGatewayEvent, Task>? EventReceived { get; set; }
        public event Action<Shard>? ShardCreated;

        public IReadOnlyDictionary<int, Shard> Shards => _shards;
        public User? User => _shards.Values.Select(s => s.User).FirstOrDefault(s => s != null);
        public ApplicationPartial? Application => _shards.Values.Select(s => s.Application).FirstOrDefault(s => s != null);

        public async Task Start(GatewayInfo.Bot info)
        {
            await Start(info.Url, 0, info.Shards - 1, info.Shards, info.SessionStartLimit.MaxConcurrency);
        }

        public async Task Start(string url, int shardMin, int shardMax, int shardTotal, int recommendedConcurrency)
        {
            _ratelimiter = GetRateLimiter(recommendedConcurrency);

            var shardCount = shardMax - shardMin + 1;
            _logger.Information("Starting {ShardCount} of {ShardTotal} shards (#{ShardMin}-#{ShardMax}) at {Url}",
                shardCount, shardTotal, shardMin, shardMax, url);
            for (var i = shardMin; i <= shardMax; i++)
                CreateAndAddShard(url, new ShardInfo(i, shardTotal));

            await StartShards();
        }
        private async Task StartShards()
        {
            _logger.Information("Connecting shards...");
            foreach (var shard in _shards.Values)
                await shard.Start();
        }

        private void CreateAndAddShard(string url, ShardInfo shardInfo)
        {
            var shard = new Shard(_gatewaySettings, shardInfo, _ratelimiter!, url, _logger);
            shard.OnEventReceived += evt => OnShardEventReceived(shard, evt);
            _shards[shardInfo.ShardId] = shard;

            ShardCreated?.Invoke(shard);
        }

        private async Task OnShardEventReceived(Shard shard, IGatewayEvent evt)
        {
            if (EventReceived != null)
                await EventReceived(shard, evt);
        }

        private int GetActualShardConcurrency(int recommendedConcurrency)
        {
            if (_gatewaySettings.MaxShardConcurrency == null)
                return recommendedConcurrency;

            return Math.Min(_gatewaySettings.MaxShardConcurrency.Value, recommendedConcurrency);
        }

        private IGatewayRatelimiter GetRateLimiter(int recommendedConcurrency)
        {
            if (_gatewaySettings.GatewayQueueUrl != null)
            {
                return new TwilightGatewayRatelimiter(_logger, _gatewaySettings.GatewayQueueUrl);
            }

            var concurrency = GetActualShardConcurrency(recommendedConcurrency);
            return new LocalGatewayRatelimiter(_logger, concurrency);
        }
    }
}