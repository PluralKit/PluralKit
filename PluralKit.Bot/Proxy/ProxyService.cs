using System;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    public class ProxyService {
        public static readonly TimeSpan MessageDeletionDelay = TimeSpan.FromMilliseconds(1000);  
        
        private LogChannelService _logChannel;
        private IDataStore _data;
        private ILogger _logger;
        private WebhookExecutorService _webhookExecutor;
        private ProxyTagParser _parser;
        private Autoproxier _autoproxier;
        
        public ProxyService(LogChannelService logChannel, IDataStore data, ILogger logger, WebhookExecutorService webhookExecutor, ProxyTagParser parser, Autoproxier autoproxier)
        {
            _logChannel = logChannel;
            _data = data;
            _webhookExecutor = webhookExecutor;
            _parser = parser;
            _autoproxier = autoproxier;
            _logger = logger.ForContext<ProxyService>();
        }

        public async Task<ProxyMatch?> TryGetMatch(DiscordMessage message, SystemGuildSettings systemGuildSettings, CachedAccount account, bool allowAutoproxy)
        {
            // First, try parsing by tags
            if (_parser.TryParse(message.Content, account.Members, out var tagMatch))
            {
                // If the content is blank (and we don't have any attachments), someone just sent a message that happens
                // to be equal to someone else's tags. This doesn't count! Proceed to autoproxy in that case.
                var isEdgeCase = tagMatch.Content.Trim().Length == 0 && message.Attachments.Count == 0;
                if (!isEdgeCase) return tagMatch;
            }
            
            // Then, if AP is enabled, try finding an autoproxy match
            if (allowAutoproxy)
                return await _autoproxier.TryAutoproxy(new Autoproxier.AutoproxyContext
                {
                    Account = account,
                    AutoproxyMember = systemGuildSettings.AutoproxyMember,
                    Content = message.Content,
                    GuildId = message.Channel.GuildId,
                    Mode = systemGuildSettings.AutoproxyMode,
                    SenderId = message.Author.Id
                });
            
            // Didn't find anything :(
            return null;
        }

        public async Task HandleMessageAsync(DiscordClient client, GuildConfig guild, CachedAccount account, DiscordMessage message, bool allowAutoproxy)
        {
            // Early checks
            if (message.Channel.Guild == null) return;
            if (guild.Blacklist.Contains(message.ChannelId)) return;
            var systemSettingsForGuild = account.SettingsForGuild(message.Channel.GuildId);
            if (!systemSettingsForGuild.ProxyEnabled) return;
            if (!await EnsureBotPermissions(message.Channel)) return;

            // Find a proxy match (either with tags or autoproxy), bail if we couldn't find any
            if (!(await TryGetMatch(message, systemSettingsForGuild, account, allowAutoproxy) is { } match))
                return;
            
            // Can't proxy a message with no content and no attachment
            if (match.Content.Trim().Length == 0 && message.Attachments.Count == 0)
                return;

            var memberSettingsForGuild = account.SettingsForMemberGuild(match.Member.Id, message.Channel.GuildId);

            // Find and check proxied name
            var proxyName = match.Member.ProxyName(account.System.Tag, memberSettingsForGuild.DisplayName);
            if (proxyName.Length < 2) throw Errors.ProxyNameTooShort(proxyName);
            if (proxyName.Length > Limits.MaxProxyNameLength) throw Errors.ProxyNameTooLong(proxyName);

            // Find proxy avatar (server avatar -> member avatar -> system avatar)
            var proxyAvatar = memberSettingsForGuild.AvatarUrl ?? match.Member.AvatarUrl ?? account.System.AvatarUrl;
            
            // Execute the webhook!
            var hookMessage = await _webhookExecutor.ExecuteWebhook(message.Channel, proxyName, proxyAvatar,
                await SanitizeEveryoneMaybe(message, match.ProxyContent),
                message.Attachments
            );

            // Store the message in the database, and log it in the log channel (if applicable)
            await _data.AddMessage(message.Author.Id, hookMessage, message.Channel.GuildId, message.Channel.Id, message.Id, match.Member);
            await _logChannel.LogMessage(client, account.System, match.Member, hookMessage, message.Id, message.Channel, message.Author, match.Content, guild);

            // Wait a second or so before deleting the original message
            await Task.Delay(MessageDeletionDelay);

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
    }
}
