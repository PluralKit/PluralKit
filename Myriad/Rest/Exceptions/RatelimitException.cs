using System;

using Myriad.Rest.Ratelimit;

namespace Myriad.Rest.Exceptions
{
    public class RatelimitException: Exception
    {
        public RatelimitException(string? message) : base(message) { }
    }

    public class RatelimitBucketExhaustedException: RatelimitException
    {
        public RatelimitBucketExhaustedException(Bucket bucket, TimeSpan retryAfter) : base(
            "Rate limit bucket exhausted, request blocked")
        {
            Bucket = bucket;
            RetryAfter = retryAfter;
        }

        public Bucket Bucket { get; }
        public TimeSpan RetryAfter { get; }
    }

    public class GloballyRatelimitedException: RatelimitException
    {
        public GloballyRatelimitedException() : base("Global rate limit hit") { }
    }
}