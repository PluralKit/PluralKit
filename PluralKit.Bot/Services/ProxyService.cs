using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Dapper;
using Discord;
using Discord.Rest;
using Discord.Webhook;
using Discord.WebSocket;

namespace PluralKit.Bot
{
    class ProxyDatabaseResult
    {
        public PKSystem System;
        public PKMember Member;
    }

    class ProxyMatch {
        public PKMember Member;
        public PKSystem System;
        public string InnerText;

        public string ProxyName => Member.Name + (System.Tag != null ? " " + System.Tag : "");
    }

    class ProxyService {
        private IDiscordClient _client;
        private IDbConnection _connection;
        private LogChannelService _logger;
        private MessageStore _messageStorage;

        private ConcurrentDictionary<ulong, Lazy<Task<IWebhook>>> _webhooks;
        
        public ProxyService(IDiscordClient client, IDbConnection connection, LogChannelService logger, MessageStore messageStorage)
        {
            this._client = client;
            this._connection = connection;
            this._logger = logger;
            this._messageStorage = messageStorage;

            _webhooks = new ConcurrentDictionary<ulong, Lazy<Task<IWebhook>>>();
        }

        private ProxyMatch GetProxyTagMatch(string message, IEnumerable<ProxyDatabaseResult> potentials) {
            // TODO: add detection of leading @mention

            // Sort by specificity (ProxyString length desc = prefix+suffix length desc = inner message asc = more specific proxy first!)
            var ordered = potentials.OrderByDescending(p => p.Member.ProxyString.Length);
            foreach (var potential in ordered) {
                var prefix = potential.Member.Prefix ?? "";
                var suffix = potential.Member.Suffix ?? "";

                if (message.StartsWith(prefix) && message.EndsWith(suffix)) {
                    var inner = message.Substring(prefix.Length, message.Length - prefix.Length - suffix.Length);
                    return new ProxyMatch { Member = potential.Member, System = potential.System, InnerText = inner };
                }
            }
            return null;
        }

        public async Task HandleMessageAsync(IMessage message) {
            var results = await _connection.QueryAsync<PKMember, PKSystem, ProxyDatabaseResult>("select members.*, systems.* from members, systems, accounts where members.system = systems.id and accounts.system = systems.id and accounts.uid = @Uid and (members.prefix != null or members.suffix != null)", (member, system) => new ProxyDatabaseResult { Member = member, System = system }, new { Uid = message.Author.Id });

            // Find a member with proxy tags matching the message
            var match = GetProxyTagMatch(message.Content, results);
            if (match == null) return;

            // Fetch a webhook for this channel, and send the proxied message
            var webhook = await GetWebhookByChannelCaching(message.Channel as ITextChannel);
            var hookMessage = await ExecuteWebhook(webhook, match.InnerText, match.ProxyName, match.Member.AvatarUrl, message.Attachments.FirstOrDefault());

            // Store the message in the database, and log it in the log channel (if applicable)
            await _messageStorage.Store(message.Author.Id, hookMessage.Id, hookMessage.Channel.Id, match.Member);
            await _logger.LogMessage(match.System, match.Member, hookMessage, message.Author);

            // Wait a second or so before deleting the original message
            await Task.Delay(1000);
            await message.DeleteAsync();
        }

        private async Task<IMessage> ExecuteWebhook(IWebhook webhook, string text, string username, string avatarUrl, IAttachment attachment) {
            var client = new DiscordWebhookClient(webhook);

            ulong messageId;
            if (attachment != null) {
                using (var stream = await WebRequest.CreateHttp(attachment.Url).GetRequestStreamAsync()) {
                    messageId = await client.SendFileAsync(stream, filename: attachment.Filename, text: text, username: username, avatarUrl: avatarUrl);
                }
            } else {
                messageId = await client.SendMessageAsync(text, username: username, avatarUrl: avatarUrl);
            }
            return await webhook.Channel.GetMessageAsync(messageId);
        }

        private async Task<IWebhook> GetWebhookByChannelCaching(ITextChannel channel) {
            // We cache the webhook through a Lazy<Task<T>>, this way we make sure to only create one webhook per channel
            // TODO: make sure this is sharding-safe. Intuition says yes, since one channel is guaranteed to only be handled by one shard, but best to make sure
            var webhookFactory = _webhooks.GetOrAdd(channel.Id, new Lazy<Task<IWebhook>>(() => FindWebhookByChannel(channel)));
            return await webhookFactory.Value;
        }

        private async Task<IWebhook> FindWebhookByChannel(ITextChannel channel) {
            IWebhook webhook;

            webhook = (await channel.GetWebhooksAsync()).FirstOrDefault(IsWebhookMine);
            if (webhook != null) return webhook;

            webhook = await channel.CreateWebhookAsync("PluralKit Proxy Webhook");
            return webhook;
        }

        private bool IsWebhookMine(IWebhook arg)
        {
            return arg.Creator.Id == this._client.CurrentUser.Id && arg.Name == "PluralKit Proxy Webhook";
        }

        public async Task HandleReactionAddedAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            // Make sure it's the right emoji (red X)
            if (reaction.Emote.Name != "\u274C") return;

            // Find the message in the database
            var storedMessage = await _messageStorage.Get(message.Id);
            if (storedMessage == null) return; // (if we can't, that's ok, no worries)

            // Make sure it's the actual sender of that message deleting the message
            if (storedMessage.SenderId != reaction.UserId) return;

            try {
                // Then, fetch the Discord message and delete that
                // TODO: this could be faster if we didn't bother fetching it and just deleted it directly
                // somehow through REST?
                await (await message.GetOrDownloadAsync()).DeleteAsync();
            } catch (NullReferenceException) {
                // Message was deleted before we got to it... cool, no problem, lmao
            }

            // Finally, delete it from our database.
            await _messageStorage.Delete(message.Id);
        }

        public async Task HandleMessageDeletedAsync(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            await _messageStorage.Delete(message.Id);
        }
    }
}