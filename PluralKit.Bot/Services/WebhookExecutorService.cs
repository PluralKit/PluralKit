using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using App.Metrics;

using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

using Humanizer;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using Serilog;

namespace PluralKit.Bot
{
    public class WebhookExecutionErrorOnDiscordsEnd: Exception {
    }
    
    public class WebhookRateLimited: WebhookExecutionErrorOnDiscordsEnd {
        // Exceptions for control flow? don't mind if I do
        // TODO: rewrite both of these as a normal exceptional return value (0?) in case of error to be discarded by caller 
    }
    
    public class WebhookExecutorService
    {
        private readonly WebhookCacheService _webhookCache;
        private readonly ILogger _logger;
        private readonly IMetrics _metrics;
        private readonly HttpClient _client;

        public WebhookExecutorService(IMetrics metrics, WebhookCacheService webhookCache, ILogger logger, HttpClient client)
        {
            _metrics = metrics;
            _webhookCache = webhookCache;
            _client = client;
            _logger = logger.ForContext<WebhookExecutorService>();
        }

        public async Task<ulong> ExecuteWebhook(DiscordChannel channel, string name, string avatarUrl, string content, IReadOnlyList<DiscordAttachment> attachments, bool allowEveryone)
        {
            _logger.Verbose("Invoking webhook in channel {Channel}", channel.Id);
            
            // Get a webhook, execute it
            var webhook = await _webhookCache.GetWebhook(channel);
            var id = await ExecuteWebhookInner(channel, webhook, name, avatarUrl, content, attachments, allowEveryone);
            
            // Log the relevant metrics
            _metrics.Measure.Meter.Mark(BotMetrics.MessagesProxied);
            _logger.Information("Invoked webhook {Webhook} in channel {Channel}", webhook.Id,
                channel.Id);
            
            return id;
        }

        private async Task<ulong> ExecuteWebhookInner(DiscordChannel channel, DiscordWebhook webhook, string name, string avatarUrl, string content,
            IReadOnlyList<DiscordAttachment> attachments, bool allowEveryone, bool hasRetried = false)
        {
            content = content.Truncate(2000);
            
            var dwb = new DiscordWebhookBuilder();
            dwb.WithUsername(FixClyde(name).Truncate(80));
            dwb.WithContent(content);
            dwb.AddMentions(content.ParseAllMentions(allowEveryone, channel.Guild));
            if (!string.IsNullOrWhiteSpace(avatarUrl)) 
                dwb.WithAvatarUrl(avatarUrl);
            
            var attachmentChunks = ChunkAttachmentsOrThrow(attachments, 8 * 1024 * 1024);
            if (attachmentChunks.Count > 0)
            {
                _logger.Information("Invoking webhook with {AttachmentCount} attachments totalling {AttachmentSize} MiB in {AttachmentChunks} chunks", attachments.Count, attachments.Select(a => a.FileSize).Sum() / 1024 / 1024, attachmentChunks.Count);
                await AddAttachmentsToBuilder(dwb, attachmentChunks[0]);
            }
            
            DiscordMessage response;
            using (_metrics.Measure.Timer.Time(BotMetrics.WebhookResponseTime)) {
                try
                {
                    response = await webhook.ExecuteAsync(dwb);
                }
                catch (JsonReaderException)
                {
                    // This happens sometimes when we hit a CloudFlare error (or similar) on Discord's end
                    // Nothing we can do about this - happens sometimes under server load, so just drop the message and give up
                    throw new WebhookExecutionErrorOnDiscordsEnd();
                }
                catch (NotFoundException e)
                {
                    var errorText = e.WebResponse?.Response;
                    if (errorText != null && errorText.Contains("10015") && !hasRetried)
                    {
                        // Error 10015 = "Unknown Webhook" - this likely means the webhook was deleted
                        // but is still in our cache. Invalidate, refresh, try again
                        _logger.Warning("Error invoking webhook {Webhook} in channel {Channel}", webhook.Id, webhook.ChannelId);
                        
                        var newWebhook = await _webhookCache.InvalidateAndRefreshWebhook(channel, webhook);
                        return await ExecuteWebhookInner(channel, newWebhook, name, avatarUrl, content, attachments, allowEveryone, hasRetried: true);
                    }

                    throw;
                }
            } 

            // We don't care about whether the sending succeeds, and we don't want to *wait* for it, so we just fork it off
            var _ = TrySendRemainingAttachments(webhook, name, avatarUrl, attachmentChunks);

            return response.Id;
        }

        private async Task TrySendRemainingAttachments(DiscordWebhook webhook, string name, string avatarUrl, IReadOnlyList<IReadOnlyCollection<DiscordAttachment>> attachmentChunks)
        {
            if (attachmentChunks.Count <= 1) return;

            for (var i = 1; i < attachmentChunks.Count; i++)
            {
                var dwb = new DiscordWebhookBuilder();
                if (avatarUrl != null) dwb.WithAvatarUrl(avatarUrl);
                dwb.WithUsername(name);
                await AddAttachmentsToBuilder(dwb, attachmentChunks[i]);
                await webhook.ExecuteAsync(dwb);
            }
        }

        private async Task AddAttachmentsToBuilder(DiscordWebhookBuilder dwb, IReadOnlyCollection<DiscordAttachment> attachments)
        {
            async Task<(DiscordAttachment, Stream)> GetStream(DiscordAttachment attachment)
            {
                var attachmentResponse = await _client.GetAsync(attachment.Url, HttpCompletionOption.ResponseHeadersRead);
                return (attachment, await attachmentResponse.Content.ReadAsStreamAsync());
            }
            
            foreach (var (attachment, attachmentStream) in await Task.WhenAll(attachments.Select(GetStream)))
                dwb.AddFile(attachment.FileName, attachmentStream);
        }

        private IReadOnlyList<IReadOnlyCollection<DiscordAttachment>> ChunkAttachmentsOrThrow(
            IReadOnlyList<DiscordAttachment> attachments, int sizeThreshold)
        {
            // Splits a list of attachments into "chunks" of at most 8MB each
            // If any individual attachment is larger than 8MB, will throw an error
            var chunks = new List<List<DiscordAttachment>>();
            var list = new List<DiscordAttachment>();
            
            foreach (var attachment in attachments)
            {
                if (attachment.FileSize >= sizeThreshold) throw Errors.AttachmentTooLarge;

                if (list.Sum(a => a.FileSize) + attachment.FileSize >= sizeThreshold)
                {
                    chunks.Add(list);
                    list = new List<DiscordAttachment>();
                }
                
                list.Add(attachment);
            }

            if (list.Count > 0) chunks.Add(list);
            return chunks;
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