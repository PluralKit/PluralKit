using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Serilog;

namespace Myriad.Rest.Ratelimit
{
    public class BucketManager: IDisposable
    {
        private static readonly TimeSpan StaleBucketTimeout = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan PruneWorkerInterval = TimeSpan.FromMinutes(1);
        private readonly ConcurrentDictionary<(string key, ulong major), Bucket> _buckets = new();

        private readonly ConcurrentDictionary<string, string> _endpointKeyMap = new();
        private readonly ConcurrentDictionary<string, int> _knownKeyLimits = new();

        private readonly ILogger _logger;

        private readonly Task _worker;
        private readonly CancellationTokenSource _workerCts = new();

        public BucketManager(ILogger logger)
        {
            _logger = logger.ForContext<BucketManager>();
            _worker = PruneWorker(_workerCts.Token);
        }

        public void Dispose()
        {
            _workerCts.Dispose();
            _worker.Dispose();
        }

        public Bucket? GetBucket(string endpoint, ulong major)
        {
            if (!_endpointKeyMap.TryGetValue(endpoint, out var key))
                return null;

            if (_buckets.TryGetValue((key, major), out var bucket))
                return bucket;

            if (!_knownKeyLimits.TryGetValue(key, out var knownLimit))
                return null;

            _logger.Debug("Creating new bucket {BucketKey}/{BucketMajor} with limit {KnownLimit}", key, major, knownLimit);
            return _buckets.GetOrAdd((key, major),
                k => new Bucket(_logger, k.Item1, k.Item2, knownLimit));
        }

        public void UpdateEndpointInfo(string endpoint, string key, int? limit)
        {
            _endpointKeyMap[endpoint] = key;

            if (limit != null)
                _knownKeyLimits[key] = limit.Value;
        }

        private async Task PruneWorker(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PruneWorkerInterval, ct);
                PruneStaleBuckets(DateTimeOffset.UtcNow);
            }
        }

        private void PruneStaleBuckets(DateTimeOffset now)
        {
            foreach (var (key, bucket) in _buckets)
            {
                if (now - bucket.LastUsed <= StaleBucketTimeout)
                    continue;

                _logger.Debug("Pruning unused bucket {BucketKey}/{BucketMajor} (last used at {BucketLastUsed})",
                    bucket.Key, bucket.Major, bucket.LastUsed);
                _buckets.TryRemove(key, out _);
            }
        }
    }
}