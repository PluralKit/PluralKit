using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Net;
using Discord.WebSocket;

using NodaTime;
using NodaTime.Extensions;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    class ProxyMatch {
        public PKMember Member;
        public PKSystem System;
        public ProxyTag? ProxyTags;
        public string InnerText;
    }

    class ProxyService {
        private DiscordShardedClient _client;
        private LogChannelService _logChannel;
        private IDataStore _data;
        private EmbedService _embeds;
        private ILogger _logger;
        private WebhookExecutorService _webhookExecutor;
        
        public ProxyService(DiscordShardedClient client, LogChannelService logChannel, IDataStore data, EmbedService embeds, ILogger logger, WebhookExecutorService webhookExecutor)
        {
            _client = client;
            _logChannel = logChannel;
            _data = data;
            _embeds = embeds;
            _webhookExecutor = webhookExecutor;
            _logger = logger.ForContext<ProxyService>();
        }

        private ProxyMatch GetProxyTagMatch(string message, PKSystem system, IEnumerable<PKMember> potentialMembers)
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
            var ordered = potentialMembers.SelectMany(m => m.ProxyTags.Select(tag => (tag, m))).OrderByDescending(p => p.Item1.ProxyString.Length);
            foreach (var (tag, match) in ordered)
            {
                if (tag.Prefix == null && tag.Suffix == null) continue;

                var prefix = tag.Prefix ?? "";
                var suffix = tag.Suffix ?? "";

                var isMatch = message.Length >= prefix.Length + suffix.Length 
                              && message.StartsWith(prefix) && message.EndsWith(suffix);
                
                // Special case for image-only proxies and proxy tags with spaces
                if (!isMatch && message.Trim() == prefix.TrimEnd() + suffix.TrimStart())
                {
                    isMatch = true;
                    message = prefix + suffix; // To get around substring errors
                }

                if (isMatch) {
                    var inner = message.Substring(prefix.Length, message.Length - prefix.Length - suffix.Length);
                    if (leadingMention != null) inner = $"{leadingMention} {inner}";
                    return new ProxyMatch { Member = match, System = system, InnerText = inner, ProxyTags = tag};
                }
            }

            return null;
        }

        public async Task HandleMessageAsync(GuildConfig guild, CachedAccount account, IMessage message)
        {
            // Bail early if this isn't in a guild channel
            if (!(message.Channel is ITextChannel channel)) return;
            
            // Find a member with proxy tags matching the message
            var match = GetProxyTagMatch(message.Content, account.System, account.Members);

            // O(n) lookup since n is small (max ~100 in prod) and we're more constrained by memory (for a dictionary) here
            var systemSettingsForGuild = account.SettingsForGuild(channel.GuildId);
            
            // If we didn't get a match by proxy tags, try to get one by autoproxy
            // Also try if we *did* get a match, but there's no inner text. This happens if someone sends a message that
            // is equal to someone else's tags, and messages like these should be autoproxied if possible
            if (match == null || (match.InnerText.Trim().Length == 0 && message.Attachments.Count == 0))
                match = await GetAutoproxyMatch(account, systemSettingsForGuild, message, channel);
            
            // If we still haven't found any, just yeet
            if (match == null) return;
            
            // And make sure the channel's not blacklisted from proxying.
            if (guild.Blacklist.Contains(channel.Id)) return;
            
            // Make sure the system hasn't blacklisted the guild either
            if (!systemSettingsForGuild.ProxyEnabled) return;
            
            // We know message.Channel can only be ITextChannel as PK doesn't work in DMs/groups
            // Afterwards we ensure the bot has the right permissions, otherwise bail early
            if (!await EnsureBotPermissions(channel)) return;
            
            // Can't proxy a message with no content and no attachment
            if (match.InnerText.Trim().Length == 0 && message.Attachments.Count == 0)
                return;

            var memberSettingsForGuild = account.SettingsForMemberGuild(match.Member.Id, channel.GuildId);
            
            // Get variables in order and all
            var proxyName = match.Member.ProxyName(match.System.Tag, memberSettingsForGuild.DisplayName);
            var avatarUrl = match.Member.AvatarUrl ?? match.System.AvatarUrl;
            
            // If the name's too long (or short), bail
            if (proxyName.Length < 2) throw Errors.ProxyNameTooShort(proxyName);
            if (proxyName.Length > Limits.MaxProxyNameLength) throw Errors.ProxyNameTooLong(proxyName);

            // Add the proxy tags into the proxied message if that option is enabled
            // Also check if the member has any proxy tags - some cases autoproxy can return a member with no tags
            var messageContents = (match.Member.KeepProxy && match.ProxyTags.HasValue)
                ? $"{match.ProxyTags.Value.Prefix}{match.InnerText}{match.ProxyTags.Value.Suffix}"
                : match.InnerText;
            
            // Sanitize @everyone, but only if the original user wouldn't have permission to
            messageContents = SanitizeEveryoneMaybe(message, messageContents);
            
            // Execute the webhook itself
            var hookMessageId = await _webhookExecutor.ExecuteWebhook(
                channel,
                proxyName, avatarUrl,
                messageContents,
                message.Attachments
            );

            // Store the message in the database, and log it in the log channel (if applicable)
            await _data.AddMessage(message.Author.Id, hookMessageId, channel.GuildId, message.Channel.Id, message.Id, match.Member);
            await _logChannel.LogMessage(match.System, match.Member, hookMessageId, message.Id, message.Channel as IGuildChannel, message.Author, match.InnerText, guild);

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

        private async Task<ProxyMatch> GetAutoproxyMatch(CachedAccount account, SystemGuildSettings guildSettings, IMessage message, IGuildChannel channel)
        {
            // For now we use a backslash as an "escape character", subject to change later
            if ((message.Content ?? "").TrimStart().StartsWith("\\")) return null; 
            
            PKMember member = null;
            // Figure out which member to proxy as
            switch (guildSettings.AutoproxyMode)
            {
                case AutoproxyMode.Off:
                    // Autoproxy off, bail
                    return null;
                case AutoproxyMode.Front:
                    // Front mode: just use the current first fronter
                    member = await _data.GetFirstFronter(account.System);
                    break;
                case AutoproxyMode.Latch:
                    // Latch mode: find last proxied message, use *that* member
                    var msg = await _data.GetLastMessageInGuild(message.Author.Id, channel.GuildId);
                    if (msg == null) return null; // No message found

                    // If the message is older than 6 hours, ignore it and force the sender to "refresh" a proxy
                    // This can be revised in the future, it's a preliminary value.
                    var timestamp = SnowflakeUtils.FromSnowflake(msg.Message.Mid).ToInstant();
                    var timeSince = SystemClock.Instance.GetCurrentInstant() - timestamp;
                    if (timeSince > Duration.FromHours(6)) return null;
                    
                    member = msg.Member;
                    break;
                case AutoproxyMode.Member:
                    // Member mode: just use that member
                    // O(n) lookup since n is small (max 1000 de jure) and we're more constrained by memory (for a dictionary) here
                    member = account.Members.FirstOrDefault(m => m.Id == guildSettings.AutoproxyMember);
                    break;
            }

            // If we haven't found the member (eg. front mode w/ no fronter), bail again
            if (member == null) return null;
            return new ProxyMatch
            {
                System = account.System,
                Member = member,
                // Autoproxying members with no proxy tags is possible, return the correct result
                ProxyTags = member.ProxyTags.Count > 0 ? member.ProxyTags.First() : (ProxyTag?) null,
                InnerText = message.Content
            };
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

            // If we can't send messages at all, just bail immediately.
            // TODO: can you have ManageMessages and *not* SendMessages? What happens then?
            if (!permissions.SendMessages && !permissions.ManageMessages) return false;

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
                case "\U0001F514": // Bell
                case "\U0001F6CE": // Bellhop bell
                case "\U0001F3D3": // Ping pong paddle (lol)
                case "\u23F0": // Alarm clock
                case "\u2757": // Exclamation mark
                    return HandleMessagePingByReaction(message, channel, reaction.UserId, reaction.Emote);
                default:
                    return Task.CompletedTask;
            }
        }

        private async Task HandleMessagePingByReaction(Cacheable<IUserMessage, ulong> message,
                                                       ISocketMessageChannel channel, ulong userWhoReacted,
                                                       IEmote reactedEmote)
        {

            // Find the message in the DB
            var msg = await _data.GetMessage(message.Id);
            if (msg == null) return;

            var realMessage = await message.GetOrDownloadAsync();
            var embed = new EmbedBuilder()
                .WithDescription($"[Jump to pinged message]({realMessage.GetJumpUrl()})");

            await channel.SendMessageAsync($"Psst, **{msg.Member.DisplayName ?? msg.Member.Name}** (<@{msg.Message.Sender}>), you have been pinged by <@{userWhoReacted}>.", embed: embed.Build());
            
            // Finally remove the original reaction (if we can)
            var user = await _client.Rest.GetUserAsync(userWhoReacted);
            if (user != null && await realMessage.Channel.HasPermission(ChannelPermission.ManageMessages))
                await realMessage.RemoveReactionAsync(reactedEmote, user);
        }

        private async Task HandleMessageQueryByReaction(Cacheable<IUserMessage, ulong> message, ulong userWhoReacted, IEmote reactedEmote)
        {
            // Find the user who sent the reaction, so we can DM them
            var user = await _client.Rest.GetUserAsync(userWhoReacted);
            if (user == null) return;

            // Find the message in the DB
            var msg = await _data.GetMessage(message.Id);
            if (msg == null) return;

            // DM them the message card
            try
            {
                await user.SendMessageAsync(embed: await _embeds.CreateMessageInfoEmbed(msg));
            }
            catch (HttpException e) when (e.DiscordCode == 50007)
            {
                // Ignore exception if it means we don't have DM permission to this user
                // not much else we can do here :/
            }

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
    }
}