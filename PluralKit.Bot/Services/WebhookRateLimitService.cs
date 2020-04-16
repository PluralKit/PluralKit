using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;

using Discord;

using NodaTime;

using Serilog;

namespace PluralKit.Bot
{
    // Simplified rate limit handler for webhooks only, disregards most bucket functionality since scope is limited and just denies requests if too fast.
    public class WebhookRateLimitService
    {
        private ILogger _logger;
        private ConcurrentDictionary<ulong, WebhookRateLimitInfo> _info = new ConcurrentDictionary<ulong, WebhookRateLimitInfo>();

        public WebhookRateLimitService(ILogger logger)
        {
            _logger = logger.ForContext<WebhookRateLimitService>();
        }

        public int CacheSize => _info.Count;

        public bool TryExecuteWebhook(IWebhook webhook)
        {
            // If we have nothing saved, just allow it (we'll save something once the response returns)
            if (!_info.TryGetValue(webhook.Id, out var info)) return true;
            
            // If we're past the reset time, allow the request and update the bucket limit
            if (SystemClock.Instance.GetCurrentInstant() > info.resetTime)
            {
                if (!info.hasResetTimeExpired)
                    info.remaining = info.maxLimit;
                info.hasResetTimeExpired = true;
                
                // We can hit this multiple times if many requests are in flight before a real one gets "back", so we still
                // decrement the remaining request count, this basically "blacklists" the channel given continuous spam until *one* of the requests come back with new rate limit headers
                info.remaining--;

                return true;
            }

            // If we don't have any more requests left, deny the request
            if (info.remaining == 0)
            {
                _logger.Debug("Rate limit bucket for {Webhook} out of requests, denying request", webhook.Id);
                return false;
            }
            
            // Otherwise, decrement the request count and allow the request
            info.remaining--;
            return true;
        }

        public void UpdateRateLimitInfo(IWebhook webhook, HttpResponseMessage response)
        {
            var info = _info.GetOrAdd(webhook.Id, _ => new WebhookRateLimitInfo());

            if (int.TryParse(GetHeader(response, "X-RateLimit-Limit"), out var limit))
                info.maxLimit = limit;

            // Max "safe" is way above UNIX timestamp values, and we get fractional seconds, hence the double
            // but need culture/format specifiers to get around Some Locales (cough, my local PC) having different settings for decimal point...
            // We also use Reset-After to avoid issues with clock desync between us and Discord's server, this way it's all relative (plus latency errors work in our favor)    
            if (double.TryParse(GetHeader(response, "X-RateLimit-Reset-After"), NumberStyles.Float, CultureInfo.InvariantCulture, out var resetTimestampDelta))
            {
                var resetTime = SystemClock.Instance.GetCurrentInstant() + Duration.FromSeconds(resetTimestampDelta);
                if (resetTime > info.resetTime)
                {
                    // Set to the *latest* reset value we have (for safety), since we rely on relative times this can jitter a bit
                    info.resetTime = resetTime;
                    info.hasResetTimeExpired = false;
                }
            }
            
            if (int.TryParse(GetHeader(response, "X-RateLimit-Remaining"), out var remainingRequests))
                // Overwrite a negative "we don't know" value with whatever we just got
                // Otherwise, *lower* remaining requests takes precedence
                if (info.remaining < 0 || remainingRequests < info.remaining)
                    info.remaining = remainingRequests;
            
            _logger.Debug("Updated rate limit information for {Webhook}, bucket has {RequestsRemaining} requests remaining, reset in {ResetTime}", webhook.Id, info.remaining, info.resetTime - SystemClock.Instance.GetCurrentInstant());
            
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                // 429, we're *definitely* out of requests
                info.remaining = 0;
                _logger.Warning("Got 429 Too Many Requests when invoking webhook {Webhook}, next bucket reset in {ResetTime}", webhook.Id, info.resetTime - SystemClock.Instance.GetCurrentInstant());
            }
        }

        public void GarbageCollect()
        {
            _logger.Information("Garbage-collecting webhook rate limit buckets...");
            
            var collected = 0;
            foreach (var channel in _info.Keys)
            {
                if (!_info.TryGetValue(channel, out var info)) continue;
                
                // Remove all keys that expired more than an hour ago (and of course, haven't been reset)
                if (info.resetTime < SystemClock.Instance.GetCurrentInstant() - Duration.FromHours(1))
                    if (_info.TryRemove(channel, out _)) collected++;
            }
            
            _logger.Information("Garbage-collected {ChannelCount} channels from the webhook rate limit buckets.", collected);
        }

        private string GetHeader(HttpResponseMessage response, string key)
        {
            var firstPair = response.Headers.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.InvariantCultureIgnoreCase));
            return firstPair.Value?.FirstOrDefault(); // If key is missing, default value is null
        }

        private class WebhookRateLimitInfo
        {
            public Instant resetTime;
            public bool hasResetTimeExpired;
            public int remaining = -1;
            public int maxLimit = 0;
        } 
    }
}