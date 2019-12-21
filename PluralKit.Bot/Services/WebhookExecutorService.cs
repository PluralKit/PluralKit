using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using App.Metrics;

using Discord;

using Humanizer;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Serilog;

namespace PluralKit.Bot
{
    public class WebhookExecutorService: IDisposable
    {
        private WebhookCacheService _webhookCache;
        private ILogger _logger;
        private IMetrics _metrics;
        private HttpClient _client;

        public WebhookExecutorService(IMetrics metrics, WebhookCacheService webhookCache, ILogger logger)
        {
            _metrics = metrics;
            _webhookCache = webhookCache;
            _logger = logger.ForContext<WebhookExecutorService>();
            _client = new HttpClient();
        }

        public async Task<ulong> ExecuteWebhook(ITextChannel channel, string name, string avatarUrl, string content, IAttachment attachment)
        {
            _logger.Verbose("Invoking webhook in channel {Channel}", channel.Id);
            
            // Get a webhook, execute it
            var webhook = await _webhookCache.GetWebhook(channel);
            var id = await ExecuteWebhookInner(webhook, name, avatarUrl, content, attachment);
            
            // Log the relevant metrics
            _metrics.Measure.Meter.Mark(BotMetrics.MessagesProxied);
            _logger.Information("Invoked webhook {Webhook} in channel {Channel}", webhook.Id,
                channel.Id);
            
            return id;
        }

        private async Task<ulong> ExecuteWebhookInner(IWebhook webhook, string name, string avatarUrl, string content,
            IAttachment attachment, bool hasRetried = false)
        {

            var mfd = new MultipartFormDataContent();
            mfd.Add(new StringContent(content.Truncate(2000)), "content");
            mfd.Add(new StringContent(FixClyde(name).Truncate(80)), "username");
            if (avatarUrl != null) mfd.Add(new StringContent(avatarUrl), "avatar_url");

            if (attachment != null)
            {
                var attachmentResponse = await _client.GetAsync(attachment.Url);
                var attachmentStream = await attachmentResponse.Content.ReadAsStreamAsync();
                mfd.Add(new StreamContent(attachmentStream), "file", attachment.Filename);
            }

            HttpResponseMessage response;
            using (_metrics.Measure.Timer.Time(BotMetrics.WebhookResponseTime))
                response = await _client.PostAsync($"{DiscordConfig.APIUrl}webhooks/{webhook.Id}/{webhook.Token}?wait=true", mfd);

            // TODO: are there cases where an error won't also return a parseable JSON object?
            var responseJson = JsonConvert.DeserializeObject<JObject>(await response.Content.ReadAsStringAsync());
            if (responseJson.ContainsKey("code"))
            {
                if (responseJson["code"].Value<int>() == 10015 && !hasRetried)
                {
                    // Error 10015 = "Unknown Webhook" - this likely means the webhook was deleted
                    // but is still in our cache. Invalidate, refresh, try again
                    _logger.Warning("Error invoking webhook {Webhook} in channel {Channel}", webhook.Id, webhook.ChannelId);
                    return await ExecuteWebhookInner(await _webhookCache.InvalidateAndRefreshWebhook(webhook), name, avatarUrl, content, attachment, hasRetried: true);
                }
                
                // TODO: look into what this actually throws, and if this is the correct handling
                response.EnsureSuccessStatusCode();
            }
            
            // At this point we're sure we have a 2xx status code, so just assume success
            // TODO: can we do this without a round-trip to a string?
            return responseJson["id"].Value<ulong>();
        }

        private string FixClyde(string name)
        {
            // Check if the name contains "Clyde" - if not, do nothing
            var match = Regex.Match(name, "clyde", RegexOptions.IgnoreCase);
            if (!match.Success) return name;

            // Put a hair space (\u200A) between the "c" and the "lyde" in the match to avoid Discord matching it
            // since Discord blocks webhooks containing the word "Clyde"... for some reason. /shrug
            return name.Substring(0, match.Index + 1) + '\u200A' + name.Substring(match.Index + 1);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}