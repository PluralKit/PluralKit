using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

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
        private MessageStore _messageStorage;
        private EmbedService _embeds;
        private ILogger _logger;
        private WebhookExecutorService _webhookExecutor;
        private ProxyCacheService _cache;

        private HttpClient _httpClient;

        public ProxyService(IDiscordClient client, LogChannelService logChannel, MessageStore messageStorage, EmbedService embeds, ILogger logger, ProxyCacheService cache, WebhookExecutorService webhookExecutor)
        {
            _client = client;
            _logChannel = logChannel;
            _messageStorage = messageStorage;
            _embeds = embeds;
            _cache = cache;
            _webhookExecutor = webhookExecutor;
            _logger = logger.ForContext<ProxyService>();

            _httpClient = new HttpClient();
        }

        private ProxyMatch GetProxyTagMatch(string message, IEnumerable<ProxyCacheService.ProxyDatabaseResult> potentials)
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

            //Convert emotes that are not on the guild to inline links
            messageContents = ConvertEmotesToInlineLinks((message.Channel as ITextChannel), messageContents);
            
            // Execute the webhook itself
            var hookMessageId = await _webhookExecutor.ExecuteWebhook(
                (ITextChannel) message.Channel,
                proxyName, avatarUrl,
                messageContents,
                message.Attachments.FirstOrDefault()
            );

            // Store the message in the database, and log it in the log channel (if applicable)
            await _messageStorage.Store(message.Author.Id, hookMessageId, message.Channel.Id, message.Id, match.Member);
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

        private static string ConvertEmotesToInlineLinks(ITextChannel channel, string sanitizedMessageContents)
        {
            MatchCollection emote_matches = Regex.Matches(sanitizedMessageContents, @"<(a?):([a-zA-Z0-9_]+):(\d+)>");
            string convertedEmoteMessageContents = sanitizedMessageContents;
            if(emote_matches.Count > 0){
                IReadOnlyCollection<GuildEmote> emotes = channel.Guild.Emotes;
                foreach (Match emote_match in emote_matches){
                    Emote parsed_emote;
                    bool ifParsed = Emote.TryParse(emote_match.Value, out parsed_emote);
                    if (ifParsed && IsEmoteNotPresent(emotes, parsed_emote)){
                        convertedEmoteMessageContents = convertedEmoteMessageContents.Replace(parsed_emote.ToString(), $"[:{parsed_emote.Name}:]({parsed_emote.Url})");
                        
                        //Since an inlink link version of an emote is MUCH longer than a normal emote, we run the risk of exceeding the 2000 char limit.
                        if (convertedEmoteMessageContents.Length > Limits.MaxMessageLength){
                            //We went over 2000 char. Abort conversion proccess and return the original message contents
                            return sanitizedMessageContents;
                        }
                    }   
                }
            }
            return convertedEmoteMessageContents;
        }

        private static bool IsEmoteNotPresent(IReadOnlyCollection<GuildEmote> server_emotes, Emote message_emote)
        {
            foreach (var server_emote in server_emotes){
                if (server_emote.Equals(message_emote)){
                    return false;
                }
            }
            return true;
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
            // Don't delete messages from the store if they aren't webhooks
            // Non-webhook messages will never be stored anyway.
            // If we're not sure (eg. message outside of cache), delete just to be sure.
            if (message.HasValue && !message.Value.Author.IsWebhook) return;
            await _messageStorage.Delete(message.Id);
        }

        public async Task HandleMessageBulkDeleteAsync(IReadOnlyCollection<Cacheable<IMessage, ulong>> messages, IMessageChannel channel)
        {
            _logger.Information("Bulk deleting {Count} messages in channel {Channel}", messages.Count, channel.Id);
            await _messageStorage.BulkDelete(messages.Select(m => m.Id).ToList());
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}