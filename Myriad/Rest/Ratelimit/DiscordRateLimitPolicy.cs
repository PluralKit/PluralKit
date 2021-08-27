using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Polly;

namespace Myriad.Rest.Ratelimit
{
    public class DiscordRateLimitPolicy: AsyncPolicy<HttpResponseMessage>
    {
        public const string EndpointContextKey = "Endpoint";
        public const string MajorContextKey = "Major";

        private readonly Ratelimiter _ratelimiter;

        public DiscordRateLimitPolicy(Ratelimiter ratelimiter, PolicyBuilder<HttpResponseMessage>? policyBuilder = null)
            : base(policyBuilder)
        {
            _ratelimiter = ratelimiter;
        }

        protected override async Task<HttpResponseMessage> ImplementationAsync(
            Func<Context, CancellationToken, Task<HttpResponseMessage>> action, Context context, CancellationToken ct,
            bool continueOnCapturedContext)
        {
            if (!context.TryGetValue(EndpointContextKey, out var endpointObj) || !(endpointObj is string endpoint))
                throw new ArgumentException("Must provide endpoint in Polly context");

            if (!context.TryGetValue(MajorContextKey, out var majorObj) || !(majorObj is ulong major))
                throw new ArgumentException("Must provide major in Polly context");

            // Check rate limit, throw if we're not allowed...
            _ratelimiter.AllowRequestOrThrow(endpoint, major, DateTimeOffset.Now);

            // We're OK, push it through
            var response = await action(context, ct).ConfigureAwait(continueOnCapturedContext);

            // Update rate limit state with headers
            var headers = RatelimitHeaders.Parse(response);
            _ratelimiter.HandleResponse(headers, endpoint, major);

            return response;
        }
    }
}