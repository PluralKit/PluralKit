using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    public class ProxyService
    {
        public static readonly TimeSpan MessageDeletionDelay = TimeSpan.FromMilliseconds(1000);

        private LogChannelService _logChannel;
        private DbConnectionFactory _db;
        private IDataStore _data;
        private ILogger _logger;
        private WebhookExecutorService _webhookExecutor;
        private readonly ProxyMatcher _matcher;

        public ProxyService(LogChannelService logChannel, IDataStore data, ILogger logger,
                            WebhookExecutorService webhookExecutor, DbConnectionFactory db, ProxyMatcher matcher)
        {
            _logChannel = logChannel;
            _data = data;
            _webhookExecutor = webhookExecutor;
            _db = db;
            _matcher = matcher;
            _logger = logger.ForContext<ProxyService>();
        }

        public async Task HandleIncomingMessage(DiscordMessage message, bool allowAutoproxy)
        {
            // Quick context checks to quit early
            if (!IsMessageValid(message)) return;

            // Fetch members and try to match to a specific member
            var members = await FetchProxyMembers(message.Author.Id, message.Channel.GuildId);
            if (!_matcher.TryMatch(members, out var match, message.Content, message.Attachments.Count > 0,
                allowAutoproxy)) return;

            // Do some quick permission checks before going through with the proxy
            // (do channel checks *after* checking other perms to make sure we don't spam errors when eg. channel is blacklisted)
            if (!IsProxyValid(message, match)) return;
            if (!await CheckBotPermissionsOrError(message.Channel)) return;
            if (!CheckProxyNameBoundsOrError(match)) return;

            // Everything's in order, we can execute the proxy!
            await ExecuteProxy(message, match);
        }
        
        private async Task ExecuteProxy(DiscordMessage trigger, ProxyMatch match)
        {
            // Send the webhook
            var id = await _webhookExecutor.ExecuteWebhook(trigger.Channel, match.Member.ProxyName, match.Member.ProxyAvatar,
                match.Content, trigger.Attachments);
            
            // Handle post-proxy actions
            await _data.AddMessage(trigger.Author.Id, trigger.Channel.GuildId, trigger.Channel.Id, id, trigger.Id, match.Member.MemberId);
            await _logChannel.LogMessage(match, trigger, id);

            // Wait a second or so before deleting the original message
            await Task.Delay(MessageDeletionDelay);
            try
            {
                await trigger.DeleteAsync();
            }
            catch (NotFoundException)
            {
                // If it's already deleted, we just log and swallow the exception
                _logger.Warning("Attempted to delete already deleted proxy trigger message {Message}", trigger.Id);
            }
        }
        
        private async Task<IReadOnlyCollection<ProxyMember>> FetchProxyMembers(ulong account, ulong guild)
        {
            await using var conn = await _db.Obtain();
            var members = await conn.QueryAsync<ProxyMember>("proxy_info",
                new {account_id = account, guild_id = guild}, commandType: CommandType.StoredProcedure);
            return members.ToList();
        }

        private async Task<bool> CheckBotPermissionsOrError(DiscordChannel channel)
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
        
        private bool CheckProxyNameBoundsOrError(ProxyMatch match)
        {
            var proxyName = match.Member.ProxyName;
            if (proxyName.Length < 2) throw Errors.ProxyNameTooShort(proxyName);
            if (proxyName.Length > Limits.MaxProxyNameLength) throw Errors.ProxyNameTooLong(proxyName);
            
            // TODO: this never returns false as it throws instead, should this happen?
            return true;
        }
        
        private bool IsMessageValid(DiscordMessage message)
        {
            return
                // Must be a guild text channel
                message.Channel.Type == ChannelType.Text &&
                
                // Must not be a system message
                message.MessageType == MessageType.Default &&
                !(message.Author.IsSystem ?? false) &&
                
                // Must not be a bot or webhook message
                !message.WebhookMessage &&
                !message.Author.IsBot &&
                
                // Must have either an attachment or content (or both, but not neither) 
                (message.Attachments.Count > 0 || (message.Content != null && message.Content.Trim().Length > 0));
        }

        private bool IsProxyValid(DiscordMessage message, ProxyMatch match)
        {
            return
                // System and member must have proxying enabled in this guild
                match.Member.ProxyEnabled &&
                
                // Channel must not be blacklisted here
                !match.Member.ChannelBlacklist.Contains(message.ChannelId);
        }
    }
}