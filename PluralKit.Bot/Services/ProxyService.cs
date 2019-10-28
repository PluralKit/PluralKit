using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Discord;
using Discord.Net;
using Discord.WebSocket;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    class ProxyMatch {
        public PKMember Member;
        public PKSystem System;
        public string InnerText;
    }

    class ProxyService: IDisposable {
        private IDiscordClient _client;
        private LogChannelService _logChannel;
        private IDataStore _data;
        private EmbedService _embeds;
        private ILogger _logger;
        private WebhookExecutorService _webhookExecutor;
        private ProxyCacheService _cache;

        private HttpClient _httpClient;

        public ProxyService(IDiscordClient client, LogChannelService logChannel, IDataStore data, EmbedService embeds, ILogger logger, ProxyCacheService cache, WebhookExecutorService webhookExecutor)
        {
            _client = client;
            _logChannel = logChannel;
            _data = data;
            _embeds = embeds;
            _cache = cache;
            _webhookExecutor = webhookExecutor;
            _logger = logger.ForContext<ProxyService>();

            _httpClient = new HttpClient();
        }

        private ProxyMatch GetProxyTagMatch(string message, IEnumerable<ProxyCacheService.ProxyDatabaseResult> potentialMembers)
        {
            // If the message starts with a @mention, and then proceeds to have proxy tags,
            // extract the mention and place it inside the inner message
            // eg. @Ske [text] => [@Ske text]
            int matchStartPosition = 0;
            string leadingMention = null;
            if (Utils.HasMentionPrefix(message, ref matchStartPosition, out _))
            {
                leadingMention = message.Substring(0, matchStartPosition);
                message = message.Substring(matchStartPosition);
            }

            // Flatten and sort by specificity (ProxyString length desc = prefix+suffix length desc = inner message asc = more specific proxy first!)
            var ordered = potentialMembers.SelectMany(m => m.Member.ProxyTags.Select(tag => (tag, m))).OrderByDescending(p => p.Item1.ProxyString);
            foreach (var (tag, match) in ordered)
            {
                if (tag.Prefix == null && tag.Suffix == null) continue;

                var prefix = tag.Prefix ?? "";
                var suffix = tag.Suffix ?? "";

                if (message.Length >= prefix.Length + suffix.Length && message.StartsWith(prefix) && message.EndsWith(suffix)) {
                    var inner = message.Substring(prefix.Length, message.Length - prefix.Length - suffix.Length);
                    if (leadingMention != null) inner = $"{leadingMention} {inner}";
                    return new ProxyMatch { Member = match.Member, System = match.System, InnerText = inner };
                }
            }

            return null;
        }

        public async Task HandleMessageAsync(IMessage message)
        {
            // Bail early if this isn't in a guild channel
            if (!(message.Channel is ITextChannel)) return;

            var results = await _cache.GetResultsFor(message.Author.Id);

            // Find a member with proxy tags matching the message
            var match = GetProxyTagMatch(message.Content, results);
            if (match == null) return;

            // We know message.Channel can only be ITextChannel as PK doesn't work in DMs/groups
            // Afterwards we ensure the bot has the right permissions, otherwise bail early
            if (!await EnsureBotPermissions(message.Channel as ITextChannel)) return;

            // Can't proxy a message with no content and no attachment
            if (match.InnerText.Trim().Length == 0 && message.Attachments.Count == 0)
                return;
            
            // Get variables in order and all
            var proxyName = match.Member.ProxyName(match.System.Tag);
            var avatarUrl = match.Member.AvatarUrl ?? match.System.AvatarUrl;
            
            // If the name's too long (or short), bail
            if (proxyName.Length < 2) throw Errors.ProxyNameTooShort(proxyName);
            if (proxyName.Length > Limits.MaxProxyNameLength) throw Errors.ProxyNameTooLong(proxyName);
            
            // Sanitize @everyone, but only if the original user wouldn't have permission to
            var messageContents = SanitizeEveryoneMaybe(message, match.InnerText);
            
            // Execute the webhook itself
            var hookMessageId = await _webhookExecutor.ExecuteWebhook(
                (ITextChannel) message.Channel,
                proxyName, avatarUrl,
                messageContents,
                message.Attachments.FirstOrDefault()
            );

            // Store the message in the database, and log it in the log channel (if applicable)
            await _data.AddMessage(message.Author.Id, hookMessageId, message.Channel.Id, message.Id, match.Member);
            await _logChannel.LogMessage(match.System, match.Member, hookMessageId, message.Id, message.Channel as IGuildChannel, message.Author, match.InnerText);

            // Wait a second or so before deleting the original message
            await Task.Delay(1000);

            try
            {
                await message.DeleteAsync();
            }
            catch (HttpException)
            {
                // If it's already deleted, we just log and swallow the exception
                _logger.Warning("Attempted to delete already deleted proxy trigger message {Message}", message.Id);
            }
        }

        private static string SanitizeEveryoneMaybe(IMessage message, string messageContents)
        {
            var senderPermissions = ((IGuildUser) message.Author).GetPermissions(message.Channel as IGuildChannel);
            if (!senderPermissions.MentionEveryone) return messageContents.SanitizeEveryone();
            return messageContents;
        }

        private async Task<bool> EnsureBotPermissions(ITextChannel channel)
        {
            var guildUser = await channel.Guild.GetCurrentUserAsync();
            var permissions = guildUser.GetPermissions(channel);

            if (!permissions.ManageWebhooks)
            {
                // todo: PKError-ify these
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
            var msg = await _data.GetMessage(message.Id);
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
            var storedMessage = await _data.GetMessage(message.Id);
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
            await _data.DeleteMessage(message.Id);
        }

        public async Task HandleMessageDeletedAsync(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            // Don't delete messages from the store if they aren't webhooks
            // Non-webhook messages will never be stored anyway.
            // If we're not sure (eg. message outside of cache), delete just to be sure.
            if (message.HasValue && !message.Value.Author.IsWebhook) return;
            await _data.DeleteMessage(message.Id);
        }

        public async Task HandleMessageBulkDeleteAsync(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages, IMessageChannel channel)
        {
            _logger.Information("Bulk deleting {Count} messages in channel {Channel}", messages.Count, channel.Id);
            await _data.DeleteMessagesBulk(messages.Select(m => m.Id).ToList());
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}