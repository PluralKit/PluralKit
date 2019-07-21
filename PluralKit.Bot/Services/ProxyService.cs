using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using App.Metrics;
using Dapper;
using Discord;
using Discord.Net;
using Discord.Webhook;
using Discord.WebSocket;
using Serilog;

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

    class ProxyService: IDisposable {
        private IDiscordClient _client;
        private DbConnectionFactory _conn;
        private LogChannelService _logChannel;
        private WebhookCacheService _webhookCache;
        private MessageStore _messageStorage;
        private EmbedService _embeds;
        private IMetrics _metrics;
        private ILogger _logger;

        private HttpClient _httpClient;
        
        public ProxyService(IDiscordClient client, WebhookCacheService webhookCache, DbConnectionFactory conn, LogChannelService logChannel, MessageStore messageStorage, EmbedService embeds, IMetrics metrics, ILogger logger)
        {
            _client = client;
            _webhookCache = webhookCache;
            _conn = conn;
            _logChannel = logChannel;
            _messageStorage = messageStorage;
            _embeds = embeds;
            _metrics = metrics;
            _logger = logger.ForContext<ProxyService>();
            
            _httpClient = new HttpClient();
        }

        private ProxyMatch GetProxyTagMatch(string message, IEnumerable<ProxyDatabaseResult> potentials)
        {
            // If the message starts with a @mention, and then proceeds to have proxy tags,
            // extract the mention and place it inside the inner message
            // eg. @Ske [text] => [@Ske text]
            int matchStartPosition = 0;
            string leadingMention = null;
            if (Utils.HasMentionPrefix(message, ref matchStartPosition))
            {
                leadingMention = message.Substring(0, matchStartPosition);
                message = message.Substring(matchStartPosition);
            }

            // Sort by specificity (ProxyString length desc = prefix+suffix length desc = inner message asc = more specific proxy first!)
            var ordered = potentials.OrderByDescending(p => p.Member.ProxyString.Length);
            foreach (var potential in ordered)
            {
                if (potential.Member.Prefix == null && potential.Member.Suffix == null) continue;
                
                var prefix = potential.Member.Prefix ?? "";
                var suffix = potential.Member.Suffix ?? "";

                if (message.Length >= prefix.Length + suffix.Length && message.StartsWith(prefix) && message.EndsWith(suffix)) {
                    var inner = message.Substring(prefix.Length, message.Length - prefix.Length - suffix.Length);
                    if (leadingMention != null) inner = $"{leadingMention} {inner}";
                    return new ProxyMatch { Member = potential.Member, System = potential.System, InnerText = inner };
                }
            }
            
            return null;
        }

        public async Task HandleMessageAsync(IMessage message)
        {
            IEnumerable<ProxyDatabaseResult> results;
            using (var conn = await _conn.Obtain())
            {
                results = await conn.QueryAsync<PKMember, PKSystem, ProxyDatabaseResult>(
                    "select members.*, systems.* from members, systems, accounts where members.system = systems.id and accounts.system = systems.id and accounts.uid = @Uid",
                    (member, system) =>
                        new ProxyDatabaseResult {Member = member, System = system}, new {Uid = message.Author.Id});
            }

            // Find a member with proxy tags matching the message
            var match = GetProxyTagMatch(message.Content, results);
            if (match == null) return;
            
            // We know message.Channel can only be ITextChannel as PK doesn't work in DMs/groups
            // Afterwards we ensure the bot has the right permissions, otherwise bail early
            if (!await EnsureBotPermissions(message.Channel as ITextChannel)) return;
            
            // Can't proxy a message with no content and no attachment
            if (match.InnerText.Trim().Length == 0 && message.Attachments.Count == 0)
                return;
            
            // Fetch a webhook for this channel, and send the proxied message
            var webhook = await _webhookCache.GetWebhook(message.Channel as ITextChannel);
            var hookMessageId = await ExecuteWebhook(webhook, match.InnerText, match.ProxyName, match.Member.AvatarUrl, message.Attachments.FirstOrDefault());

            // Store the message in the database, and log it in the log channel (if applicable)
            await _messageStorage.Store(message.Author.Id, hookMessageId, message.Channel.Id, match.Member);
            await _logChannel.LogMessage(match.System, match.Member, hookMessageId, message.Channel as IGuildChannel, message.Author, match.InnerText);

            // Wait a second or so before deleting the original message
            await Task.Delay(1000);

            try
            {
                await message.DeleteAsync();
            } catch (HttpException) {} // If it's already deleted, we just swallow the exception
        }

        private async Task<bool> EnsureBotPermissions(ITextChannel channel)
        {
            var guildUser = await channel.Guild.GetCurrentUserAsync();
            var permissions = guildUser.GetPermissions(channel);

            if (!permissions.ManageWebhooks)
            {
                await channel.SendMessageAsync(
                    $"{Emojis.Error} PluralKit does not have the *Manage Webhooks* permission in this channel, and thus cannot proxy messages. Please contact a server administrator to remedy this.");
                return false;
            }
            
            if (!permissions.ManageMessages)
            {
                await channel.SendMessageAsync(
                    $"{Emojis.Error} PluralKit does not have the *Manage Messages* permission in this channel, and thus cannot delete the original trigger message. Please contact a server administrator to remedy this.");
                return false;
            }

            return true;
        }

        private async Task<ulong> ExecuteWebhook(IWebhook webhook, string text, string username, string avatarUrl, IAttachment attachment)
        {
            username = FixClyde(username);
            
            // TODO: DiscordWebhookClient's ctor does a call to GetWebhook that may be unnecessary, see if there's a way to do this The Hard Way :tm:
            // TODO: this will probably crash if there are multiple consecutive failures, perhaps have a loop instead?
            DiscordWebhookClient client;
            try
            {
                client = new DiscordWebhookClient(webhook);
            }
            catch (InvalidOperationException)
            {
                // TODO: does this leak internal stuff in the (now-invalid) client?
                
                // webhook was deleted or invalid
                webhook = await _webhookCache.InvalidateAndRefreshWebhook(webhook);
                client = new DiscordWebhookClient(webhook);
            }

            // TODO: clean this entire block up
            using (client)
            {
                ulong messageId;

                try
                {
                    if (attachment != null)
                    {
                        using (var stream = await _httpClient.GetStreamAsync(attachment.Url))
                        {
                            messageId = await client.SendFileAsync(stream, filename: attachment.Filename, text: text,
                                username: username, avatarUrl: avatarUrl);
                        }
                    }
                    else
                    {
                        messageId = await client.SendMessageAsync(text, username: username, avatarUrl: avatarUrl);
                    }

                    _logger.Information("Invoked webhook {Webhook} in channel {Channel}", webhook.Id,
                        webhook.ChannelId);

                    // Log it in the metrics
                    _metrics.Measure.Meter.Mark(BotMetrics.MessagesProxied, "success");
                }
                catch (HttpException e)
                {
                    _logger.Warning(e, "Error invoking webhook {Webhook} in channel {Channel}", webhook.Id,
                        webhook.ChannelId);

                    // Log failure in metrics and rethrow (we still need to cancel everything else)
                    _metrics.Measure.Meter.Mark(BotMetrics.MessagesProxied, "failure");
                    throw;
                }
                
                // TODO: figure out a way to return the full message object (without doing a GetMessageAsync call, which
                // doesn't work if there's no permission to)
                return messageId;
            }
        }

        public Task HandleReactionAddedAsync(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            // Dispatch on emoji
            switch (reaction.Emote.Name)
            {
                case "\u274C": // Red X
                    return HandleMessageDeletionByReaction(message, reaction.UserId);
                case "\u2753": // Red question mark
                case "\u2754": // White question mark
                    return HandleMessageQueryByReaction(message, reaction.UserId, reaction.Emote);
                default:
                    return Task.CompletedTask;
            }
        }

        private async Task HandleMessageQueryByReaction(Cacheable<IUserMessage, ulong> message, ulong userWhoReacted, IEmote reactedEmote)
        {
            // Find the user who sent the reaction, so we can DM them
            var user = await _client.GetUserAsync(userWhoReacted);
            if (user == null) return;

            // Find the message in the DB
            var msg = await _messageStorage.Get(message.Id);
            if (msg == null) return;
            
            // DM them the message card
            await user.SendMessageAsync(embed: await _embeds.CreateMessageInfoEmbed(msg));
            
            // And finally remove the original reaction (if we can)
            var msgObj = await message.GetOrDownloadAsync();
            if (await msgObj.Channel.HasPermission(ChannelPermission.ManageMessages))
                await msgObj.RemoveReactionAsync(reactedEmote, user);
        }

        public async Task HandleMessageDeletionByReaction(Cacheable<IUserMessage, ulong> message, ulong userWhoReacted)
        {
            // Find the message in the database
            var storedMessage = await _messageStorage.Get(message.Id);
            if (storedMessage == null) return; // (if we can't, that's ok, no worries)

            // Make sure it's the actual sender of that message deleting the message
            if (storedMessage.Message.Sender != userWhoReacted) return;

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

        public async Task HandleMessageBulkDeleteAsync(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages, IMessageChannel channel)
        {
            _logger.Information("Bulk deleting {Count} messages in channel {Channel}", messages.Count, channel.Id);
            await _messageStorage.BulkDelete(messages.Select(m => m.Id).ToList());
        }

        private string FixClyde(string name)
        {
            var match = Regex.Match(name, "clyde", RegexOptions.IgnoreCase);
            if (!match.Success) return name;
            
            // Put a hair space (\u200A) between the "c" and the "lyde" in the match to avoid Discord matching it
            // since Discord blocks webhooks containing the word "Clyde"... for some reason. /shrug
            return name.Substring(0, match.Index + 1) + '\u200A' + name.Substring(match.Index + 1);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}