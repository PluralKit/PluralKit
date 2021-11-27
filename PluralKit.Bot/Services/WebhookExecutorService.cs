using System.Text.RegularExpressions;

using App.Metrics;

using Humanizer;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using Newtonsoft.Json;

using Serilog;

namespace PluralKit.Bot;

public class WebhookExecutionErrorOnDiscordsEnd: Exception { }

public class WebhookRateLimited: WebhookExecutionErrorOnDiscordsEnd
{
    // Exceptions for control flow? don't mind if I do
    // TODO: rewrite both of these as a normal exceptional return value (0?) in case of error to be discarded by caller
}

public record ProxyRequest
{
    public ulong GuildId { get; init; }
    public ulong ChannelId { get; init; }
    public ulong? ThreadId { get; init; }
    public string Name { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Content { get; init; }
    public Message.Attachment[] Attachments { get; init; }
    public int FileSizeLimit { get; init; }
    public Embed[] Embeds { get; init; }
    public bool AllowEveryone { get; init; }
}

public class WebhookExecutorService
{
    private readonly IDiscordCache _cache;
    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;
    private readonly DiscordApiClient _rest;
    private readonly WebhookCacheService _webhookCache;

    public WebhookExecutorService(IMetrics metrics, WebhookCacheService webhookCache, ILogger logger,
                                  HttpClient client, IDiscordCache cache, DiscordApiClient rest)
    {
        _metrics = metrics;
        _webhookCache = webhookCache;
        _client = client;
        _cache = cache;
        _rest = rest;
        _logger = logger.ForContext<WebhookExecutorService>();
    }

    public async Task<Message> ExecuteWebhook(ProxyRequest req)
    {
        _logger.Verbose("Invoking webhook in channel {Channel}", req.ChannelId);

        // Get a webhook, execute it
        var webhook = await _webhookCache.GetWebhook(req.ChannelId);
        var webhookMessage = await ExecuteWebhookInner(webhook, req);

        // Log the relevant metrics
        _metrics.Measure.Meter.Mark(BotMetrics.MessagesProxied);
        _logger.Information("Invoked webhook {Webhook} in channel {Channel} (thread {ThreadId})", webhook.Id,
            req.ChannelId, req.ThreadId);

        return webhookMessage;
    }

    public async Task<Message> EditWebhookMessage(ulong channelId, ulong messageId, string newContent)
    {
        var allowedMentions = newContent.ParseMentions() with
        {
            Roles = Array.Empty<ulong>(),
            Parse = Array.Empty<AllowedMentions.ParseType>()
        };

        ulong? threadId = null;
        var root = await _cache.GetRootChannel(channelId);
        if (root.Id != channelId)
            threadId = channelId;

        var webhook = await _webhookCache.GetWebhook(root.Id);

        return await _rest.EditWebhookMessage(webhook.Id, webhook.Token, messageId,
            new WebhookMessageEditRequest { Content = newContent, AllowedMentions = allowedMentions },
            threadId);
    }

    private async Task<Message> ExecuteWebhookInner(Webhook webhook, ProxyRequest req, bool hasRetried = false)
    {
        var guild = await _cache.GetGuild(req.GuildId);
        var content = req.Content.Truncate(2000);

        var allowedMentions = content.ParseMentions();
        if (!req.AllowEveryone)
            allowedMentions = allowedMentions.RemoveUnmentionableRoles(guild) with
            {
                // also clear @everyones
                Parse = Array.Empty<AllowedMentions.ParseType>()
            };

        var webhookReq = new ExecuteWebhookRequest
        {
            Username = FixProxyName(req.Name).Truncate(80),
            Content = content,
            AllowedMentions = allowedMentions,
            AvatarUrl = !string.IsNullOrWhiteSpace(req.AvatarUrl) ? req.AvatarUrl : null,
            Embeds = req.Embeds
        };

        MultipartFile[] files = null;
        var attachmentChunks = ChunkAttachmentsOrThrow(req.Attachments, req.FileSizeLimit);
        if (attachmentChunks.Count > 0)
        {
            _logger.Information(
                "Invoking webhook with {AttachmentCount} attachments totalling {AttachmentSize} MiB in {AttachmentChunks} chunks",
                req.Attachments.Length, req.Attachments.Select(a => a.Size).Sum() / 1024 / 1024,
                attachmentChunks.Count);
            files = await GetAttachmentFiles(attachmentChunks[0]);
            webhookReq.Attachments = files.Select(f => new Message.Attachment
            {
                Id = (ulong)Array.IndexOf(files, f),
                Description = f.Description,
                Filename = f.Filename
            }).ToArray();
        }

        Message webhookMessage;
        using (_metrics.Measure.Timer.Time(BotMetrics.WebhookResponseTime))
        {
            try
            {
                webhookMessage =
                    await _rest.ExecuteWebhook(webhook.Id, webhook.Token, webhookReq, files, req.ThreadId);
            }
            catch (JsonReaderException)
            {
                // This happens sometimes when we hit a CloudFlare error (or similar) on Discord's end
                // Nothing we can do about this - happens sometimes under server load, so just drop the message and give up
                throw new WebhookExecutionErrorOnDiscordsEnd();
            }
            catch (NotFoundException e)
            {
                if (e.ErrorCode == 10015 && !hasRetried)
                {
                    // Error 10015 = "Unknown Webhook" - this likely means the webhook was deleted
                    // but is still in our cache. Invalidate, refresh, try again
                    _logger.Warning("Error invoking webhook {Webhook} in channel {Channel} (thread {ThreadId})",
                        webhook.Id, webhook.ChannelId, req.ThreadId);

                    var newWebhook = await _webhookCache.InvalidateAndRefreshWebhook(req.ChannelId, webhook);
                    return await ExecuteWebhookInner(newWebhook, req, true);
                }

                throw;
            }
        }

        // We don't care about whether the sending succeeds, and we don't want to *wait* for it, so we just fork it off
        var _ = TrySendRemainingAttachments(webhook, req.Name, req.AvatarUrl, attachmentChunks, req.ThreadId);

        return webhookMessage;
    }

    private async Task TrySendRemainingAttachments(Webhook webhook, string name, string avatarUrl,
                                                   IReadOnlyList<IReadOnlyCollection<Message.Attachment>>
                                                       attachmentChunks, ulong? threadId)
    {
        if (attachmentChunks.Count <= 1) return;

        for (var i = 1; i < attachmentChunks.Count; i++)
        {
            var files = await GetAttachmentFiles(attachmentChunks[i]);
            var req = new ExecuteWebhookRequest
            {
                Username = name,
                AvatarUrl = avatarUrl,
                Attachments = files.Select(f => new Message.Attachment
                {
                    Id = (ulong)Array.IndexOf(files, f),
                    Description = f.Description,
                    Filename = f.Filename
                }).ToArray()
            };
            await _rest.ExecuteWebhook(webhook.Id, webhook.Token!, req, files, threadId);
        }
    }

    private async Task<MultipartFile[]> GetAttachmentFiles(IReadOnlyCollection<Message.Attachment> attachments)
    {
        async Task<MultipartFile> GetStream(Message.Attachment attachment)
        {
            var attachmentResponse =
                await _client.GetAsync(attachment.Url, HttpCompletionOption.ResponseHeadersRead);
            return new MultipartFile(attachment.Filename, await attachmentResponse.Content.ReadAsStreamAsync(),
                attachment.Description);
        }

        return await Task.WhenAll(attachments.Select(GetStream));
    }

    private IReadOnlyList<IReadOnlyCollection<Message.Attachment>> ChunkAttachmentsOrThrow(
        IReadOnlyList<Message.Attachment> attachments, int sizeThreshold)
    {
        // Splits a list of attachments into "chunks" of at most 8MB each
        // If any individual attachment is larger than 8MB, will throw an error
        var chunks = new List<List<Message.Attachment>>();
        var list = new List<Message.Attachment>();

        // sizeThreshold is in MB (user-readable)
        var bytesThreshold = sizeThreshold * 1024 * 1024;

        foreach (var attachment in attachments)
        {
            if (attachment.Size >= bytesThreshold) throw Errors.AttachmentTooLarge(sizeThreshold);

            if (list.Sum(a => a.Size) + attachment.Size >= bytesThreshold)
            {
                chunks.Add(list);
                list = new List<Message.Attachment>();
            }

            list.Add(attachment);
        }

        if (list.Count > 0) chunks.Add(list);
        return chunks;
    }

    private string FixProxyName(string name) => FixSingleCharacterName(FixClyde(name));

    private string FixClyde(string name)
    {
        static string Replacement(Match m) => m.Groups[1].Value + "\u200A" + m.Groups[2].Value;

        // Adds a Unicode hair space (\u200A) between the "c" and the "lyde" to avoid Discord matching it
        // since Discord blocks webhooks containing the word "Clyde"... for some reason. /shrug
        return Regex.Replace(name, "(c)(lyde)", Replacement, RegexOptions.IgnoreCase);
    }

    private string FixSingleCharacterName(string proxyName)
    {
        if (proxyName.Length == 1)
            return proxyName + "\u17b5";
        return proxyName;
    }
}