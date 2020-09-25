using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using App.Metrics;

using DSharpPlus;
using DSharpPlus.Entities;

using Serilog;

namespace PluralKit.Bot
{
    public class WebhookCacheService
    {
        public static readonly string WebhookName = "PluralKit Proxy Webhook";
            
        private readonly DiscordShardedClient _client;
        private readonly ConcurrentDictionary<ulong, Lazy<Task<DiscordWebhook>>> _webhooks;

        private readonly IMetrics _metrics;
        private readonly ILogger _logger;

        public WebhookCacheService(DiscordShardedClient client, ILogger logger, IMetrics metrics)
        {
            _client = client;
            _metrics = metrics;
            _logger = logger.ForContext<WebhookCacheService>();
            _webhooks = new ConcurrentDictionary<ulong, Lazy<Task<DiscordWebhook>>>();
        }

        public async Task<DiscordWebhook> GetWebhook(DiscordClient client, ulong channelId)
        {
            var channel = await client.GetChannel(channelId);
            if (channel == null) return null;
            if (channel.Type == ChannelType.Text) return null;
            return await GetWebhook(channel);
        }

        public async Task<DiscordWebhook> GetWebhook(DiscordChannel channel)
        {
            // We cache the webhook through a Lazy<Task<T>>, this way we make sure to only create one webhook per channel
            // If the webhook is requested twice before it's actually been found, the Lazy<T> wrapper will stop the
            // webhook from being created twice.
            Lazy<Task<DiscordWebhook>> GetWebhookTaskInner()
            {
                Task<DiscordWebhook> Factory() => GetOrCreateWebhook(channel);
                return _webhooks.GetOrAdd(channel.Id, new Lazy<Task<DiscordWebhook>>(Factory));
            }
            var lazyWebhookValue = GetWebhookTaskInner();
            
            // If we've cached a failed Task, we need to clear it and try again
            // This is so errors don't become "sticky" and *they* in turn get cached (not good)
            // although, keep in mind this block gets hit the call *after* the task failed (since we only await it below)
            if (lazyWebhookValue.IsValueCreated && lazyWebhookValue.Value.IsFaulted)
            {
                _logger.Warning(lazyWebhookValue.Value.Exception, "Cached webhook task for {Channel} faulted with below exception", channel.Id);
                
                // Specifically don't recurse here so we don't infinite-loop - if this one errors too, it'll "stick"
                // until next time this function gets hit (which is okay, probably).
                _webhooks.TryRemove(channel.Id, out _);
                lazyWebhookValue = GetWebhookTaskInner();
            }

            // It's possible to "move" a webhook to a different channel after creation
            // Here, we ensure it's actually still pointing towards the proper channel, and if not, wipe and refetch one.
            var webhook = await lazyWebhookValue.Value;
            if (webhook.ChannelId != channel.Id && webhook.ChannelId != 0) return await InvalidateAndRefreshWebhook(channel, webhook);
            return webhook;
        }

        public async Task<DiscordWebhook> InvalidateAndRefreshWebhook(DiscordChannel channel, DiscordWebhook webhook)
        {
            _logger.Information("Refreshing webhook for channel {Channel}", webhook.ChannelId);
            
            _webhooks.TryRemove(webhook.ChannelId, out _);
            return await GetWebhook(channel);
        }

        private async Task<DiscordWebhook> GetOrCreateWebhook(DiscordChannel channel)
        {
            _logger.Debug("Webhook for channel {Channel} not found in cache, trying to fetch", channel.Id);
            _metrics.Measure.Meter.Mark(BotMetrics.WebhookCacheMisses);
            
            _logger.Debug("Finding webhook for channel {Channel}", channel.Id);
            var webhooks = await FetchChannelWebhooks(channel);

            // If the channel has a webhook created by PK, just return that one
            var ourWebhook = webhooks.FirstOrDefault(IsWebhookMine);
            if (ourWebhook != null)
                return ourWebhook;
            
            // We don't have one, so we gotta create a new one
            // but first, make sure we haven't hit the webhook cap yet...
            if (webhooks.Count >= 10)
                throw new PKError("This channel has the maximum amount of possible webhooks (10) already created. A server admin must delete one or more webhooks so PluralKit can create one for proxying.");
            
            return await DoCreateWebhook(channel);
        }

        private async Task<IReadOnlyList<DiscordWebhook>> FetchChannelWebhooks(DiscordChannel channel)
        {
            try
            {
                return await channel.GetWebhooksAsync();
            }
            catch (HttpRequestException e)
            {
                _logger.Warning(e, "Error occurred while fetching webhook list");

                // This happens sometimes when Discord returns a malformed request for the webhook list
                // Nothing we can do than just assume that none exist.
                return new DiscordWebhook[0];
            }
        }

        private async Task<DiscordWebhook> FindExistingWebhook(DiscordChannel channel)
        {
            _logger.Debug("Finding webhook for channel {Channel}", channel.Id);
            try
            {
                return (await channel.GetWebhooksAsync()).FirstOrDefault(IsWebhookMine);
            }
            catch (HttpRequestException e)
            {
                _logger.Warning(e, "Error occurred while fetching webhook list");
                // This happens sometimes when Discord returns a malformed request for the webhook list
                // Nothing we can do than just assume that none exist and return null.
                return null;
            }
        }

        private Task<DiscordWebhook> DoCreateWebhook(DiscordChannel channel)
        {
            _logger.Information("Creating new webhook for channel {Channel}", channel.Id);
            return channel.CreateWebhookAsync(WebhookName);
        }

        private bool IsWebhookMine(DiscordWebhook arg) => arg.User.Id == _client.CurrentUser.Id && arg.Name == WebhookName;

        public int CacheSize => _webhooks.Count;
    }
}