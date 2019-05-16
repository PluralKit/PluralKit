using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace PluralKit.Bot
{
    public class WebhookCacheService
    {
        public static readonly string WebhookName = "PluralKit Proxy Webhook";
            
        private IDiscordClient _client;
        private ConcurrentDictionary<ulong, Lazy<Task<IWebhook>>> _webhooks;

        public WebhookCacheService(IDiscordClient client)
        {
            this._client = client;
            _webhooks = new ConcurrentDictionary<ulong, Lazy<Task<IWebhook>>>();
        }

        public async Task<IWebhook> GetWebhook(ulong channelId)
        {
            var channel = await _client.GetChannelAsync(channelId) as ITextChannel;
            if (channel == null) return null;
            return await GetWebhook(channel);
        }

        public async Task<IWebhook> GetWebhook(ITextChannel channel)
        {
            // We cache the webhook through a Lazy<Task<T>>, this way we make sure to only create one webhook per channel
            // If the webhook is requested twice before it's actually been found, the Lazy<T> wrapper will stop the
            // webhook from being created twice.
            var lazyWebhookValue =
                _webhooks.GetOrAdd(channel.Id, new Lazy<Task<IWebhook>>(() => GetOrCreateWebhook(channel)));
            return await lazyWebhookValue.Value;
        }

        private async Task<IWebhook> GetOrCreateWebhook(ITextChannel channel) =>
            await FindExistingWebhook(channel) ?? await GetOrCreateWebhook(channel);

        private async Task<IWebhook> FindExistingWebhook(ITextChannel channel) => (await channel.GetWebhooksAsync()).FirstOrDefault(IsWebhookMine);
        
        private async Task<IWebhook> DoCreateWebhook(ITextChannel channel) => await channel.CreateWebhookAsync(WebhookName);
        private bool IsWebhookMine(IWebhook arg) => arg.Creator.Id == _client.CurrentUser.Id && arg.Name == WebhookName;
    }
}