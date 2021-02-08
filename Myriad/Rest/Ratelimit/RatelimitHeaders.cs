using System;
using System.Linq;
using System.Net.Http;

namespace Myriad.Rest.Ratelimit
{
    public record RatelimitHeaders
    {
        public RatelimitHeaders() { }

        public RatelimitHeaders(HttpResponseMessage response)
        {
            ServerDate = response.Headers.Date;

            if (response.Headers.TryGetValues("X-RateLimit-Limit", out var limit))
                if (int.TryParse(limit.First(), out var limitNum))
                    Limit = limitNum;

            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining))
                if (int.TryParse(remaining!.First(), out var remainingNum))
                    Remaining = remainingNum;

            if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset))
                if (double.TryParse(reset!.First(), out var resetNum))
                    Reset = DateTimeOffset.FromUnixTimeMilliseconds((long) (resetNum * 1000));

            if (response.Headers.TryGetValues("X-RateLimit-Reset-After", out var resetAfter))
                if (double.TryParse(resetAfter!.First(), out var resetAfterNum))
                    ResetAfter = TimeSpan.FromSeconds(resetAfterNum);

            if (response.Headers.TryGetValues("X-RateLimit-Bucket", out var bucket))
                Bucket = bucket.First();

            if (response.Headers.TryGetValues("X-RateLimit-Global", out var global))
                if (bool.TryParse(global!.First(), out var globalBool))
                    Global = globalBool;
        }

        public bool Global { get; init; }
        public int? Limit { get; init; }
        public int? Remaining { get; init; }
        public DateTimeOffset? Reset { get; init; }
        public TimeSpan? ResetAfter { get; init; }
        public string? Bucket { get; init; }

        public DateTimeOffset? ServerDate { get; init; }

        public bool HasRatelimitInfo =>
            Limit != null && Remaining != null && Reset != null && ResetAfter != null && Bucket != null;
    }
}