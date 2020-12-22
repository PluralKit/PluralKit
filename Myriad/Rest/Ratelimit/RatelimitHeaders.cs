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
                Limit = int.Parse(limit!.First());

            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining))
                Remaining = int.Parse(remaining!.First());

            if (response.Headers.TryGetValues("X-RateLimit-Reset", out var reset))
                Reset = DateTimeOffset.FromUnixTimeMilliseconds((long) (double.Parse(reset!.First()) * 1000));

            if (response.Headers.TryGetValues("X-RateLimit-Reset-After", out var resetAfter))
                ResetAfter = TimeSpan.FromSeconds(double.Parse(resetAfter!.First()));

            if (response.Headers.TryGetValues("X-RateLimit-Bucket", out var bucket))
                Bucket = bucket.First();

            if (response.Headers.TryGetValues("X-RateLimit-Global", out var global))
                Global = bool.Parse(global!.First());
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