using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;

using Serilog;

namespace PluralKit.Bot
{
    public class WebhookCacheService
    {
        public static readonly string WebhookName = "PluralKit Proxy Webhook";
            
        private DiscordShardedClient _client;
        private ConcurrentDictionary<ulong, Lazy<Task<DiscordWebhook>>> _webhooks;

        private ILogger _logger;

        public WebhookCacheService(DiscordShardedClient client, ILogger logger)
        {
            _client = client;
            _logger = logger.ForContext<WebhookCacheService>();
            _webhooks = new ConcurrentDictionary<ulong, Lazy<Task<DiscordWebhook>>>();
        }

        public async Task<DiscordWebhook> GetWebhook(DiscordClient client, ulong channelId)
        {
            var channel = await client.GetChannelAsync(channelId);
            if (channel == null) return null;
            if (channel.Type == ChannelType.Text) return null;
            return await GetWebhook(channel);
        }

        public async Task<DiscordWebhook> GetWebhook(DiscordChannel channel)
        {
            // We cache the webhook through a Lazy<Task<T>>, this way we make sure to only create one webhook per channel
            // If the webhook is requested twice before it's actually been found, the Lazy<T> wrapper will stop the
            // webhook from being created twice.
            var lazyWebhookValue =    
                _webhooks.GetOrAdd(channel.Id, new Lazy<Task<DiscordWebhook>>(() => GetOrCreateWebhook(channel)));
            
            // It's possible to "move" a webhook to a different channel after creation
            // Here, we ensure it's actually still pointing towards the proper channel, and if not, wipe and refetch one.
            var webhook = await lazyWebhookValue.Value;
            if (webhook.ChannelId != channel.Id) return await InvalidateAndRefreshWebhook(channel, webhook);
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
            return await FindExistingWebhook(channel) ?? await DoCreateWebhook(channel);
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