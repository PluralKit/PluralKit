using System;
using System.Threading;

using Serilog;

namespace Myriad.Rest.Ratelimit
{
    public class Bucket
    {
        private static readonly TimeSpan Epsilon = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan FallbackDelay = TimeSpan.FromMilliseconds(200);

        private static readonly TimeSpan StaleTimeout = TimeSpan.FromSeconds(5);

        private readonly ILogger _logger;
        private readonly SemaphoreSlim _semaphore = new(1, 1);

        private DateTimeOffset? _nextReset;
        private bool _resetTimeValid;
        private bool _hasReceivedHeaders;

        public Bucket(ILogger logger, string key, ulong major, int limit)
        {
            _logger = logger.ForContext<Bucket>();

            Key = key;
            Major = major;

            Limit = limit;
            Remaining = limit;
            _resetTimeValid = false;
        }

        public string Key { get; }
        public ulong Major { get; }

        public int Remaining { get; private set; }

        public int Limit { get; private set; }

        public DateTimeOffset LastUsed { get; private set; } = DateTimeOffset.UtcNow;

        public bool TryAcquire()
        {
            LastUsed = DateTimeOffset.Now;

            try
            {
                _semaphore.Wait();

                if (Remaining > 0)
                {
                    _logger.Verbose(
                        "{BucketKey}/{BucketMajor}: Bucket has [{BucketRemaining}/{BucketLimit} left], allowing through",
                        Key, Major, Remaining, Limit);
                    Remaining--;

                    return true;
                }

                _logger.Debug("{BucketKey}/{BucketMajor}: Bucket has [{BucketRemaining}/{BucketLimit}] left, denying",
                    Key, Major, Remaining, Limit);
                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void HandleResponse(RatelimitHeaders headers)
        {
            try
            {
                _semaphore.Wait();

                _logger.Verbose("{BucketKey}/{BucketMajor}: Received rate limit headers: {@RateLimitHeaders}",
                    Key, Major, headers);

                if (headers.ResetAfter != null)
                {
                    var headerNextReset = DateTimeOffset.UtcNow + headers.ResetAfter.Value; // todo: server time
                    if (_nextReset == null || headerNextReset > _nextReset)
                    {
                        _logger.Verbose("{BucketKey}/{BucketMajor}: Received reset time {NextReset} from server (after: {NextResetAfter}, remaining: {Remaining}, local remaining: {LocalRemaining})",
                            Key, Major, headerNextReset, headers.ResetAfter.Value, headers.Remaining, Remaining);

                        _nextReset = headerNextReset;
                        _resetTimeValid = true;
                    }
                }

                if (headers.Limit != null)
                    Limit = headers.Limit.Value;

                if (headers.Remaining != null && !_hasReceivedHeaders)
                {
                    var oldRemaining = Remaining;
                    Remaining = Math.Min(headers.Remaining.Value, Remaining);

                    _logger.Debug("{BucketKey}/{BucketMajor}: Received first remaining of {HeaderRemaining}, previous local remaining is {LocalRemaining}, new local remaining is {Remaining}",
                        Key, Major, headers.Remaining.Value, oldRemaining, Remaining);
                    _hasReceivedHeaders = true;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Tick(DateTimeOffset now)
        {
            try
            {
                _semaphore.Wait();

                // If we don't have any reset data, "snap" it to now
                // This happens before first request and at this point the reset is invalid anyway, so it's fine
                // but it ensures the stale timeout doesn't trigger early by using `default` value
                if (_nextReset == null)
                    _nextReset = now;

                // If we're past the reset time *and* we haven't reset already, do that
                var timeSinceReset = now - _nextReset;
                var shouldReset = _resetTimeValid && timeSinceReset > TimeSpan.Zero;
                if (shouldReset)
                {
                    _logger.Debug("{BucketKey}/{BucketMajor}: Bucket timed out, refreshing with {BucketLimit} requests",
                        Key, Major, Limit);
                    Remaining = Limit;
                    _resetTimeValid = false;
                    return;
                }

                // We've run out of requests without having any new reset time,
                // *and* it's been longer than a set amount - add one request back to the pool and hope that one returns
                var isBucketStale = !_resetTimeValid && Remaining <= 0 && timeSinceReset > StaleTimeout;
                if (isBucketStale)
                {
                    _logger.Warning(
                        "{BucketKey}/{BucketMajor}: Bucket is stale ({StaleTimeout} passed with no rate limit info), allowing one request through",
                        Key, Major, StaleTimeout);

                    Remaining = 1;

                    // Reset the (still-invalid) reset time to now, so we don't keep hitting this conditional over and over...
                    _nextReset = now;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public TimeSpan GetResetDelay(DateTimeOffset now)
        {
            // If we don't have a valid reset time, return the fallback delay always
            // (so it'll keep spinning until we hopefully have one...)
            if (!_resetTimeValid)
                return FallbackDelay;

            var delay = (_nextReset ?? now) - now;

            // If we have a really small (or negative) value, return a fallback delay too
            if (delay < Epsilon)
                return FallbackDelay;

            return delay;
        }
    }
}