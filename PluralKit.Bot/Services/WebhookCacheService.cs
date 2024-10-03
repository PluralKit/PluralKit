using System.Collections.Concurrent;

using App.Metrics;

using Myriad.Cache;
using Myriad.Rest;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using Serilog;

namespace PluralKit.Bot;

public class WebhookCacheService
{
    public static readonly string WebhookName = "PluralKit Proxy Webhook";
    private readonly IDiscordCache _cache;
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;
    private readonly BotConfig _config;


    private readonly DiscordApiClient _rest;
    private readonly ConcurrentDictionary<ulong, Lazy<Task<Webhook>>> _webhooks;

    public WebhookCacheService(ILogger logger, IMetrics metrics, DiscordApiClient rest, IDiscordCache cache, BotConfig config)
    {
        _metrics = metrics;
        _rest = rest;
        _cache = cache;
        _config = config;
        _logger = logger.ForContext<WebhookCacheService>();
        _webhooks = new ConcurrentDictionary<ulong, Lazy<Task<Webhook>>>();
    }

    public int CacheSize => _webhooks.Count;

    public async Task<Webhook> GetWebhook(ulong channelId)
    {
        // We cache the webhook through a Lazy<Task<T>>, this way we make sure to only create one webhook per channel
        // If the webhook is requested twice before it's actually been found, the Lazy<T> wrapper will stop the
        // webhook from being created twice.
        Lazy<Task<Webhook>> GetWebhookTaskInner()
        {
            Task<Webhook> Factory() => GetOrCreateWebhook(channelId);
            return _webhooks.GetOrAdd(channelId, new Lazy<Task<Webhook>>(Factory));
        }

        var lazyWebhookValue = GetWebhookTaskInner();

        // If we've cached a failed Task, we need to clear it and try again
        // This is so errors don't become "sticky" and *they* in turn get cached (not good)
        // although, keep in mind this block gets hit the call *after* the task failed (since we only await it below)
        if (lazyWebhookValue.IsValueCreated && lazyWebhookValue.Value.IsFaulted)
        {
            _logger.Warning(lazyWebhookValue.Value.Exception,
                "Cached webhook task for {Channel} faulted with below exception", channelId);

            // Specifically don't recurse here so we don't infinite-loop - if this one errors too, it'll "stick"
            // until next time this function gets hit (which is okay, probably).
            _webhooks.TryRemove(channelId, out _);
            lazyWebhookValue = GetWebhookTaskInner();
        }

        // It's possible to "move" a webhook to a different channel after creation
        // Here, we ensure it's actually still pointing towards the proper channel, and if not, wipe and refetch one.
        var webhook = await lazyWebhookValue.Value;
        if (webhook.ChannelId != channelId && webhook.ChannelId != 0)
            return await InvalidateAndRefreshWebhook(channelId, webhook);
        return webhook;
    }

    public async Task<Webhook> InvalidateAndRefreshWebhook(ulong channelId, Webhook webhook)
    {
        // note: webhook.ChannelId may not be the same as channelId >.>
        _logger.Debug("Refreshing webhook for channel {Channel}", webhook.ChannelId);

        _webhooks.TryRemove(webhook.ChannelId, out _);
        return await GetWebhook(channelId);
    }

    private async Task<Webhook?> GetOrCreateWebhook(ulong channelId)
    {
        _logger.Debug("Webhook for channel {Channel} not found in cache, trying to fetch", channelId);
        _metrics.Measure.Meter.Mark(BotMetrics.WebhookCacheMisses);

        _logger.Debug("Finding webhook for channel {Channel}", channelId);
        var webhooks = await FetchChannelWebhooks(channelId);

        // If the channel has a webhook created by PK, just return that one
        var ourWebhook = webhooks.FirstOrDefault(hook => IsWebhookMine(hook));
        if (ourWebhook != null)
            return ourWebhook;

        // We don't have one, so we gotta create a new one
        // but first, make sure we haven't hit the webhook cap yet...
        if (webhooks.Length >= 15)
            throw new PKError(
                "This channel has the maximum amount of possible webhooks (15) already created. A server admin must delete one or more webhooks so PluralKit can create one for proxying.");

        return await DoCreateWebhook(channelId);
    }

    private async Task<Webhook[]> FetchChannelWebhooks(ulong channelId)
    {
        try
        {
            var webhooks = await _rest.GetChannelWebhooks(channelId);
            if (webhooks != null)
                return webhooks;

            // Getting a 404 / null response from the above generally means the channel type does
            // not support webhooks - this is detected elsewhere for proxying purposes, let's just
            // return an empty array here
            return new Webhook[0];
        }
        catch (HttpRequestException e)
        {
            _logger.Warning(e, "Error occurred while fetching webhook list");

            // This happens sometimes when Discord returns a malformed request for the webhook list
            // Nothing we can do than just assume that none exist.
            return new Webhook[0];
        }
    }

    private async Task<Webhook> DoCreateWebhook(ulong channelId)
    {
        _logger.Information("Creating new webhook for channel {Channel}", channelId);
        return await _rest.CreateWebhook(channelId, new CreateWebhookRequest(WebhookName));
    }

    private bool IsWebhookMine(Webhook arg) => arg.User?.Id == _config.ClientId && arg.Name == WebhookName;
}