using System;

using Myriad.Rest.Exceptions;

using Serilog;

namespace Myriad.Rest.Ratelimit
{
    public class Ratelimiter: IDisposable
    {
        private readonly BucketManager _buckets;
        private readonly ILogger _logger;

        private DateTimeOffset? _globalRateLimitExpiry;

        public Ratelimiter(ILogger logger)
        {
            _logger = logger.ForContext<Ratelimiter>();
            _buckets = new BucketManager(logger);
        }

        public void Dispose()
        {
            _buckets.Dispose();
        }

        public void AllowRequestOrThrow(string endpoint, ulong major, DateTimeOffset now)
        {
            if (IsGloballyRateLimited(now))
            {
                _logger.Warning("Globally rate limited until {GlobalRateLimitExpiry}, cancelling request",
                    _globalRateLimitExpiry);
                throw new GloballyRatelimitedException();
            }

            var bucket = _buckets.GetBucket(endpoint, major);
            if (bucket == null)
            {
                // No rate limit for this endpoint (yet), allow through
                _logger.Debug("No rate limit data for endpoint {Endpoint}, allowing through", endpoint);
                return;
            }

            bucket.Tick(now);

            if (bucket.TryAcquire())
                // We're allowed to send it! :)
                return;

            // We can't send this request right now; retrying...
            var waitTime = bucket.GetResetDelay(now);

            // add a small buffer for Timing:tm:
            waitTime += TimeSpan.FromMilliseconds(50);

            // (this is caught by a WaitAndRetry Polly handler, if configured)
            throw new RatelimitBucketExhaustedException(bucket, waitTime);
        }

        public void HandleResponse(RatelimitHeaders headers, string endpoint, ulong major)
        {
            if (!headers.HasRatelimitInfo)
                return;

            // TODO: properly calculate server time?
            if (headers.Global)
            {
                _logger.Warning(
                    "Global rate limit hit, resetting at {GlobalRateLimitExpiry} (in {GlobalRateLimitResetAfter}!",
                    _globalRateLimitExpiry, headers.ResetAfter);
                _globalRateLimitExpiry = headers.Reset;
            }
            else
            {
                // Update buckets first, then get it again, to properly "transfer" this info over to the new value
                _buckets.UpdateEndpointInfo(endpoint, headers.Bucket!, headers.Limit);

                var bucket = _buckets.GetBucket(endpoint, major);
                bucket?.HandleResponse(headers);
            }
        }

        private bool IsGloballyRateLimited(DateTimeOffset now) =>
            _globalRateLimitExpiry > now;
    }
}