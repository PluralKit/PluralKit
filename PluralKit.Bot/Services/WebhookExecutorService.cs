using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public class WebhookExecutorService
    {
        private WebhookCacheService _webhookCache;
        private ILogger _logger;
        private IMetrics _metrics;
        private HttpClient _client;

        public WebhookExecutorService(IMetrics metrics, WebhookCacheService webhookCache, ILogger logger, HttpClient client)
        {
            _metrics = metrics;
            _webhookCache = webhookCache;
            _client = client;
            _logger = logger.ForContext<WebhookExecutorService>();
        }

        public async Task<ulong> ExecuteWebhook(ITextChannel channel, string name, string avatarUrl, string content, IReadOnlyCollection<IAttachment> attachments)
        {
            _logger.Verbose("Invoking webhook in channel {Channel}", channel.Id);
            
            // Get a webhook, execute it
            var webhook = await _webhookCache.GetWebhook(channel);
            var id = await ExecuteWebhookInner(webhook, name, avatarUrl, content, attachments);
            
            // Log the relevant metrics
            _metrics.Measure.Meter.Mark(BotMetrics.MessagesProxied);
            _logger.Information("Invoked webhook {Webhook} in channel {Channel}", webhook.Id,
                channel.Id);
            
            return id;
        }

        private async Task<ulong> ExecuteWebhookInner(IWebhook webhook, string name, string avatarUrl, string content,
            IReadOnlyCollection<IAttachment> attachments, bool hasRetried = false)
        {

            using var mfd = new MultipartFormDataContent
            {
                {new StringContent(content.Truncate(2000)), "content"},
                {new StringContent(FixClyde(name).Truncate(80)), "username"}
            };
            if (avatarUrl != null) mfd.Add(new StringContent(avatarUrl), "avatar_url");

            var attachmentChunks = ChunkAttachmentsOrThrow(attachments, 8 * 1024 * 1024);
            if (attachmentChunks.Count > 0)
            {
                _logger.Information("Invoking webhook with {AttachmentCount} attachments totalling {AttachmentSize} MiB in {AttachmentChunks} chunks", attachments.Count, attachments.Select(a => a.Size).Sum() / 1024 / 1024, attachmentChunks.Count);
                await AddAttachmentsToMultipart(mfd, attachmentChunks.First());
            }

            var timerCtx = _metrics.Measure.Timer.Time(BotMetrics.WebhookResponseTime);
            using var response = await _client.PostAsync($"{DiscordConfig.APIUrl}webhooks/{webhook.Id}/{webhook.Token}?wait=true", mfd);
            timerCtx.Dispose();

            var responseString = await response.Content.ReadAsStringAsync();
            if (responseString.StartsWith("<"))
                // if the response starts with a < it's probably a CloudFlare error or similar, so just force-break
                response.EnsureSuccessStatusCode();

            var responseJson = JsonConvert.DeserializeObject<JObject>(responseString);
            if (responseJson.ContainsKey("code"))
            {
                var errorCode = responseJson["code"].Value<int>();
                if (errorCode == 10015 && !hasRetried)
                {
                    // Error 10015 = "Unknown Webhook" - this likely means the webhook was deleted
                    // but is still in our cache. Invalidate, refresh, try again
                    _logger.Warning("Error invoking webhook {Webhook} in channel {Channel}", webhook.Id, webhook.ChannelId);
                    return await ExecuteWebhookInner(await _webhookCache.InvalidateAndRefreshWebhook(webhook), name, avatarUrl, content, attachments, hasRetried: true);
                }

                if (errorCode == 40005)
                    throw Errors.AttachmentTooLarge; // should be caught by the check above but just makin' sure

                // TODO: look into what this actually throws, and if this is the correct handling
                response.EnsureSuccessStatusCode();
            }
            
            // If we have any leftover attachment chunks, send those
            if (attachmentChunks.Count > 1)
            {
                // Deliberately not adding a content, just the remaining files
                foreach (var chunk in attachmentChunks.Skip(1))
                {
                    using var mfd2 = new MultipartFormDataContent();
                    mfd2.Add(new StringContent(FixClyde(name).Truncate(80)), "username");
                    if (avatarUrl != null) mfd2.Add(new StringContent(avatarUrl), "avatar_url");
                    await AddAttachmentsToMultipart(mfd2, chunk);
                    
                    // Don't bother with ?wait, we're just kinda firehosing this stuff
                    // also don't error check, the real message itself is already sent
                    await _client.PostAsync($"{DiscordConfig.APIUrl}webhooks/{webhook.Id}/{webhook.Token}", mfd2);
                }
            }
            
            // At this point we're sure we have a 2xx status code, so just assume success
            // TODO: can we do this without a round-trip to a string?
            return responseJson["id"].Value<ulong>();
        }

        private IReadOnlyCollection<IReadOnlyCollection<IAttachment>> ChunkAttachmentsOrThrow(
            IReadOnlyCollection<IAttachment> attachments, int sizeThreshold)
        {
            // Splits a list of attachments into "chunks" of at most 8MB each
            // If any individual attachment is larger than 8MB, will throw an error
            var chunks = new List<List<IAttachment>>();
            var list = new List<IAttachment>();
            
            foreach (var attachment in attachments)
            {
                if (attachment.Size >= sizeThreshold) throw Errors.AttachmentTooLarge;

                if (list.Sum(a => a.Size) + attachment.Size >= sizeThreshold)
                {
                    chunks.Add(list);
                    list = new List<IAttachment>();
                }
                
                list.Add(attachment);
            }

            if (list.Count > 0) chunks.Add(list);
            return chunks;
        }

        private async Task AddAttachmentsToMultipart(MultipartFormDataContent content,
                                               IReadOnlyCollection<IAttachment> attachments)
        {
            async Task<(IAttachment, Stream)> GetStream(IAttachment attachment)
            {
                var attachmentResponse = await _client.GetAsync(attachment.Url, HttpCompletionOption.ResponseHeadersRead);
                return (attachment, await attachmentResponse.Content.ReadAsStreamAsync());
            }
            
            var attachmentId = 0;
            foreach (var (attachment, attachmentStream) in await Task.WhenAll(attachments.Select(GetStream)))
                content.Add(new StreamContent(attachmentStream), $"file{attachmentId++}", attachment.Filename);
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
    }
}