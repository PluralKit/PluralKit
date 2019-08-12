using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Webhook;
using Serilog;

namespace PluralKit.Bot
{
    public class WebhookCacheService: IDisposable
    {
        public class WebhookCacheEntry
        {
            internal DiscordWebhookClient Client;
            internal IWebhook Webhook;
        }
        
        public static readonly string WebhookName = "PluralKit Proxy Webhook";
            
        private IDiscordClient _client;
        private ConcurrentDictionary<ulong, Lazy<Task<WebhookCacheEntry>>> _webhooks;

        private ILogger _logger;

        public WebhookCacheService(IDiscordClient client, ILogger logger)
        {
            _client = client;
            _logger = logger.ForContext<WebhookCacheService>();
            _webhooks = new ConcurrentDictionary<ulong, Lazy<Task<WebhookCacheEntry>>>();
        }

        public async Task<WebhookCacheEntry> GetWebhook(ulong channelId)
        {
            var channel = await _client.GetChannelAsync(channelId) as ITextChannel;
            if (channel == null) return null;
            return await GetWebhook(channel);
        }

        public async Task<WebhookCacheEntry> GetWebhook(ITextChannel channel)
        {
            // We cache the webhook through a Lazy<Task<T>>, this way we make sure to only create one webhook per channel
            // If the webhook is requested twice before it's actually been found, the Lazy<T> wrapper will stop the
            // webhook from being created twice.
            var lazyWebhookValue =    
                _webhooks.GetOrAdd(channel.Id, new Lazy<Task<WebhookCacheEntry>>(() => GetOrCreateWebhook(channel)));
            
            // It's possible to "move" a webhook to a different channel after creation
            // Here, we ensure it's actually still pointing towards the proper channel, and if not, wipe and refetch one.
            var webhook = await lazyWebhookValue.Value;
            if (webhook.Webhook.ChannelId != channel.Id) return await InvalidateAndRefreshWebhook(webhook);
            return webhook;
        }

        public async Task<WebhookCacheEntry> InvalidateAndRefreshWebhook(WebhookCacheEntry webhook)
        {
            _logger.Information("Refreshing webhook for channel {Channel}", webhook.Webhook.ChannelId);
            
            _webhooks.TryRemove(webhook.Webhook.ChannelId, out _);
            return await GetWebhook(webhook.Webhook.Channel);
        }

        private async Task<WebhookCacheEntry> GetOrCreateWebhook(ITextChannel channel)
        {
            var webhook = await FindExistingWebhook(channel) ?? await DoCreateWebhook(channel);
            return await DoCreateWebhookClient(webhook);
        }
        
        private async Task<IWebhook> FindExistingWebhook(ITextChannel channel)
        {
            _logger.Debug("Finding webhook for channel {Channel}", channel.Id);
            return (await channel.GetWebhooksAsync()).FirstOrDefault(IsWebhookMine);
        }

        private Task<IWebhook> DoCreateWebhook(ITextChannel channel)
        {
            _logger.Information("Creating new webhook for channel {Channel}", channel.Id);
            return channel.CreateWebhookAsync(WebhookName);
        }

        private Task<WebhookCacheEntry> DoCreateWebhookClient(IWebhook webhook)
        {
            // DiscordWebhookClient's ctor is synchronous despite doing web calls, so we wrap it in a Task
            return Task.Run(() =>
            {
                return new WebhookCacheEntry
                {
                    Client = new DiscordWebhookClient(webhook),
                    Webhook = webhook
                };
            });
        }

        private bool IsWebhookMine(IWebhook arg) => arg.Creator.Id == _client.CurrentUser.Id && arg.Name == WebhookName;

        public int CacheSize => _webhooks.Count;

        public void Dispose()
        {
            foreach (var entry in _webhooks.Values)
                if (entry.IsValueCreated)
                    entry.Value.Result.Client.Dispose();
        }
    }
}