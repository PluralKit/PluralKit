using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;

using NodaTime;

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

    public class ProxyService {
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
            if (StringUtils.HasMentionPrefix(message, ref matchStartPosition, out _))
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

        public async Task HandleMessageAsync(DiscordClient client, GuildConfig guild, CachedAccount account, DiscordMessage message, bool doAutoProxy)
        {
            // Bail early if this isn't in a guild channel
            if (message.Channel.Guild == null) return;
            
            // Find a member with proxy tags matching the message
            var match = GetProxyTagMatch(message.Content, account.System, account.Members);

            // O(n) lookup since n is small (max ~100 in prod) and we're more constrained by memory (for a dictionary) here
            var systemSettingsForGuild = account.SettingsForGuild(message.Channel.GuildId);
            
            // If we didn't get a match by proxy tags, try to get one by autoproxy
            // Also try if we *did* get a match, but there's no inner text. This happens if someone sends a message that
            // is equal to someone else's tags, and messages like these should be autoproxied if possible
            
            // All of this should only be done if this call allows autoproxy.
            // When a normal message is sent, autoproxy is enabled, but if this method is called from a message *edit*
            // event, then autoproxy is disabled. This is so AP doesn't "retrigger" when the original message was escaped.
            if (doAutoProxy && (match == null || (match.InnerText.Trim().Length == 0 && message.Attachments.Count == 0)))
                match = await GetAutoproxyMatch(account, systemSettingsForGuild, message, message.Channel);
            
            // If we still haven't found any, just yeet
            if (match == null) return;
            
            // And make sure the channel's not blacklisted from proxying.
            if (guild.Blacklist.Contains(message.ChannelId)) return;
            
            // Make sure the system hasn't blacklisted the guild either
            if (!systemSettingsForGuild.ProxyEnabled) return;
            
            // We know message.Channel can only be ITextChannel as PK doesn't work in DMs/groups
            // Afterwards we ensure the bot has the right permissions, otherwise bail early
            if (!await EnsureBotPermissions(message.Channel)) return;
            
            // Can't proxy a message with no content and no attachment
            if (match.InnerText.Trim().Length == 0 && message.Attachments.Count == 0)
                return;

            var memberSettingsForGuild = account.SettingsForMemberGuild(match.Member.Id, message.Channel.GuildId);
            
            // Get variables in order and all
            var proxyName = match.Member.ProxyName(match.System.Tag, memberSettingsForGuild.DisplayName);
            var avatarUrl = memberSettingsForGuild.AvatarUrl ?? match.Member.AvatarUrl ?? match.System.AvatarUrl;
            
            // If the name's too long (or short), bail
            if (proxyName.Length < 2) throw Errors.ProxyNameTooShort(proxyName);
            if (proxyName.Length > Limits.MaxProxyNameLength) throw Errors.ProxyNameTooLong(proxyName);

            // Add the proxy tags into the proxied message if that option is enabled
            // Also check if the member has any proxy tags - some cases autoproxy can return a member with no tags
            var messageContents = (match.Member.KeepProxy && match.ProxyTags.HasValue)
                ? $"{match.ProxyTags.Value.Prefix}{match.InnerText}{match.ProxyTags.Value.Suffix}"
                : match.InnerText;
            
            // Sanitize @everyone, but only if the original user wouldn't have permission to
            messageContents = await SanitizeEveryoneMaybe(message, messageContents);
            
            // Execute the webhook itself
            var hookMessageId = await _webhookExecutor.ExecuteWebhook(message.Channel, proxyName, avatarUrl,
                messageContents,
                message.Attachments
            );

            // Store the message in the database, and log it in the log channel (if applicable)
            await _data.AddMessage(message.Author.Id, hookMessageId, message.Channel.GuildId, message.Channel.Id, message.Id, match.Member);
            await _logChannel.LogMessage(client, match.System, match.Member, hookMessageId, message.Id, message.Channel, message.Author, match.InnerText, guild);

            // Wait a second or so before deleting the original message
            await Task.Delay(1000);

            try
            {
                await message.DeleteAsync();
            }
            catch (NotFoundException)
            {
                // If it's already deleted, we just log and swallow the exception
                _logger.Warning("Attempted to delete already deleted proxy trigger message {Message}", message.Id);
            }
        }

        private async Task<ProxyMatch> GetAutoproxyMatch(CachedAccount account, SystemGuildSettings guildSettings, DiscordMessage message, DiscordChannel channel)
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
                    var timestamp = DiscordUtils.SnowflakeToInstant(msg.Message.Mid);
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

        private static async Task<string> SanitizeEveryoneMaybe(DiscordMessage message,
                                                                string messageContents)
        {
            var permissions = await message.Channel.PermissionsIn(message.Author);
            return (permissions & Permissions.MentionEveryone) == 0 ? messageContents.SanitizeEveryone() : messageContents;
        }

        private async Task<bool> EnsureBotPermissions(DiscordChannel channel)
        {
            var permissions = channel.BotPermissions();

            // If we can't send messages at all, just bail immediately.
            // 2020-04-22: Manage Messages does *not* override a lack of Send Messages.
            if ((permissions & Permissions.SendMessages) == 0) return false;

            if ((permissions & Permissions.ManageWebhooks) == 0)
            {
                // todo: PKError-ify these
                await channel.SendMessageAsync(
                    $"{Emojis.Error} PluralKit does not have the *Manage Webhooks* permission in this channel, and thus cannot proxy messages. Please contact a server administrator to remedy this.");
                return false;
            }

            if ((permissions & Permissions.ManageMessages) == 0)
            {
                await channel.SendMessageAsync(
                    $"{Emojis.Error} PluralKit does not have the *Manage Messages* permission in this channel, and thus cannot delete the original trigger message. Please contact a server administrator to remedy this.");
                return false;
            }

            return true;
        }

        public Task HandleReactionAddedAsync(MessageReactionAddEventArgs args)
        {
            // Dispatch on emoji
            switch (args.Emoji.Name)
            {
                case "\u274C": // Red X
                    return HandleMessageDeletionByReaction(args);
                case "\u2753": // Red question mark
                case "\u2754": // White question mark
                    return HandleMessageQueryByReaction(args);
                case "\U0001F514": // Bell
                case "\U0001F6CE": // Bellhop bell
                case "\U0001F3D3": // Ping pong paddle (lol)
                case "\u23F0": // Alarm clock
                case "\u2757": // Exclamation mark
                    return HandleMessagePingByReaction(args);
                default:
                    return Task.CompletedTask;
            }
        }

        private async Task HandleMessagePingByReaction(MessageReactionAddEventArgs args)
        {
            // Bail in DMs
            if (args.Channel.Type != ChannelType.Text) return;
            
            // Find the message in the DB
            var msg = await _data.GetMessage(args.Message.Id);
            if (msg == null) return;
            
            // Check if the pinger has permission to ping in this channel
            var guildUser = await args.Guild.GetMemberAsync(args.User.Id);
            var permissions = guildUser.PermissionsIn(args.Channel);
            
            // If they don't have Send Messages permission, bail (since PK shouldn't send anything on their behalf)
            var requiredPerms = Permissions.AccessChannels | Permissions.SendMessages;
            if ((permissions & requiredPerms) != requiredPerms) return;
            
            var embed = new DiscordEmbedBuilder().WithDescription($"[Jump to pinged message]({args.Message.JumpLink})");
            await args.Channel.SendMessageAsync($"Psst, **{msg.Member.DisplayName ?? msg.Member.Name}** (<@{msg.Message.Sender}>), you have been pinged by <@{args.User.Id}>.", embed: embed.Build());
            
            // Finally remove the original reaction (if we can)
            if (args.Channel.BotHasAllPermissions(Permissions.ManageMessages))
                await args.Message.DeleteReactionAsync(args.Emoji, args.User);
        }

        private async Task HandleMessageQueryByReaction(MessageReactionAddEventArgs args)
        {
            // Bail if not in guild
            if (args.Guild == null) return;
            
            // Find the message in the DB
            var msg = await _data.GetMessage(args.Message.Id);
            if (msg == null) return;
            
            // Get guild member so we can DM
            var member = await args.Guild.GetMemberAsync(args.User.Id);

            // DM them the message card
            try
            {
                await member.SendMessageAsync(embed: await _embeds.CreateMemberEmbed(msg.System, msg.Member, args.Guild, LookupContext.ByNonOwner));
                await member.SendMessageAsync(embed: await _embeds.CreateMessageInfoEmbed(args.Client, msg));
            }
            catch (BadRequestException)
            {
                // TODO: is this the correct exception
                // Ignore exception if it means we don't have DM permission to this user
                // not much else we can do here :/
            }

            // And finally remove the original reaction (if we can)
            await args.Message.DeleteReactionAsync(args.Emoji, args.User);
        }

        public async Task HandleMessageDeletionByReaction(MessageReactionAddEventArgs args)
        {
            // Bail if we don't have permission to delete
            if (!args.Channel.BotHasAllPermissions(Permissions.ManageMessages)) return;
            
            // Find the message in the database
            var storedMessage = await _data.GetMessage(args.Message.Id);
            if (storedMessage == null) return; // (if we can't, that's ok, no worries)

            // Make sure it's the actual sender of that message deleting the message
            if (storedMessage.Message.Sender != args.User.Id) return;

            try
            {
                await args.Message.DeleteAsync();
            } catch (NullReferenceException) {
                // Message was deleted before we got to it... cool, no problem, lmao
            }

            // Finally, delete it from our database.
            await _data.DeleteMessage(args.Message.Id);
        }

        public async Task HandleMessageDeletedAsync(MessageDeleteEventArgs args)
        {
            // Don't delete messages from the store if they aren't webhooks
            // Non-webhook messages will never be stored anyway.
            // If we're not sure (eg. message outside of cache), delete just to be sure.
            if (!args.Message.WebhookMessage) return;
            await _data.DeleteMessage(args.Message.Id);
        }

        public async Task HandleMessageBulkDeleteAsync(MessageBulkDeleteEventArgs args)
        {
            _logger.Information("Bulk deleting {Count} messages in channel {Channel}", args.Messages.Count, args.Channel.Id);
            await _data.DeleteMessagesBulk(args.Messages.Select(m => m.Id).ToList());
        }
    }
}