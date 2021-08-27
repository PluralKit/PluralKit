using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;

namespace Myriad.Rest.Ratelimit
{
    public record RatelimitHeaders
    {
        private const string LimitHeader = "X-RateLimit-Limit";
        private const string RemainingHeader = "X-RateLimit-Remaining";
        private const string ResetHeader = "X-RateLimit-Reset";
        private const string ResetAfterHeader = "X-RateLimit-Reset-After";
        private const string BucketHeader = "X-RateLimit-Bucket";
        private const string GlobalHeader = "X-RateLimit-Global";

        public bool Global { get; private set; }
        public int? Limit { get; private set; }
        public int? Remaining { get; private set; }
        public DateTimeOffset? Reset { get; private set; }
        public TimeSpan? ResetAfter { get; private set; }
        public string? Bucket { get; private set; }

        public DateTimeOffset? ServerDate { get; private set; }

        public bool HasRatelimitInfo =>
            Limit != null && Remaining != null && Reset != null && ResetAfter != null && Bucket != null;

        public RatelimitHeaders() { }

        public static RatelimitHeaders Parse(HttpResponseMessage response)
        {
            var headers = new RatelimitHeaders
            {
                ServerDate = response.Headers.Date,
                Limit = TryGetInt(response, LimitHeader),
                Remaining = TryGetInt(response, RemainingHeader),
                Bucket = TryGetHeader(response, BucketHeader)
            };


            var resetTimestamp = TryGetDouble(response, ResetHeader);
            if (resetTimestamp != null)
                headers.Reset = DateTimeOffset.FromUnixTimeMilliseconds((long)(resetTimestamp.Value * 1000));

            var resetAfterSeconds = TryGetDouble(response, ResetAfterHeader);
            if (resetAfterSeconds != null)
                headers.ResetAfter = TimeSpan.FromSeconds(resetAfterSeconds.Value);

            var global = TryGetHeader(response, GlobalHeader);
            if (global != null && bool.TryParse(global, out var globalBool))
                headers.Global = globalBool;

            return headers;
        }

        private static string? TryGetHeader(HttpResponseMessage response, string headerName)
        {
            if (!response.Headers.TryGetValues(headerName, out var values))
                return null;

            return values.FirstOrDefault();
        }

        private static int? TryGetInt(HttpResponseMessage response, string headerName)
        {
            var valueString = TryGetHeader(response, headerName);

            if (!int.TryParse(valueString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                return null;

            return value;
        }

        private static double? TryGetDouble(HttpResponseMessage response, string headerName)
        {
            var valueString = TryGetHeader(response, headerName);

            if (!double.TryParse(valueString, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return null;

            return value;
        }
    }
}