using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using App.Metrics;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    public class ProxyService
    {
        private static readonly TimeSpan MessageDeletionDelay = TimeSpan.FromMilliseconds(1000);

        private readonly LogChannelService _logChannel;
        private readonly IDatabase _db;
        private readonly IDataStore _data;
        private readonly ILogger _logger;
        private readonly WebhookExecutorService _webhookExecutor;
        private readonly ProxyMatcher _matcher;
        private readonly IMetrics _metrics;

        public ProxyService(LogChannelService logChannel, IDataStore data, ILogger logger,
                            WebhookExecutorService webhookExecutor, IDatabase db, ProxyMatcher matcher, IMetrics metrics)
        {
            _logChannel = logChannel;
            _data = data;
            _webhookExecutor = webhookExecutor;
            _db = db;
            _matcher = matcher;
            _metrics = metrics;
            _logger = logger.ForContext<ProxyService>();
        }

        public async Task<bool> HandleIncomingMessage(DiscordMessage message, MessageContext ctx, bool allowAutoproxy)
        {
            if (!ShouldProxy(message, ctx)) return false;

            // Fetch members and try to match to a specific member
            await using var conn = await _db.Obtain();

            List<ProxyMember> members;
            using (_metrics.Measure.Timer.Time(BotMetrics.ProxyMembersQueryTime))
                members = (await conn.QueryProxyMembers(message.Author.Id, message.Channel.GuildId)).ToList();
            
            if (!_matcher.TryMatch(ctx, members, out var match, message.Content, message.Attachments.Count > 0,
                allowAutoproxy)) return false;

            // Permission check after proxy match so we don't get spammed when not actually proxying
            if (!await CheckBotPermissionsOrError(message.Channel)) return false;
            if (!CheckProxyNameBoundsOrError(match.Member.ProxyName(ctx))) return false;

            // Everything's in order, we can execute the proxy!
            await ExecuteProxy(conn, message, ctx, match);
            return true;
        }

        private bool ShouldProxy(DiscordMessage msg, MessageContext ctx)
        {
            // Make sure author has a system
            if (ctx.SystemId == null) return false;
            
            // Make sure channel is a guild text channel and this is a normal message
            if (msg.Channel.Type != ChannelType.Text || msg.MessageType != MessageType.Default) return false;
            
            // Make sure author is a normal user
            if (msg.Author.IsSystem == true || msg.Author.IsBot || msg.WebhookMessage) return false;
            
            // Make sure proxying is enabled here
            if (!ctx.ProxyEnabled || ctx.InBlacklist) return false;
            
            // Make sure we have either an attachment or message content
            var isMessageBlank = msg.Content == null || msg.Content.Trim().Length == 0;
            if (isMessageBlank && msg.Attachments.Count == 0) return false;
            
            // All good!
            return true;
        }

        public async Task ExecuteProxy(IPKConnection conn, DiscordMessage trigger, MessageContext ctx,
                                        ProxyMatch match)
        {
            // Send the webhook
            var id = await _webhookExecutor.ExecuteWebhook(trigger.Channel, match.Member.ProxyName(ctx),
                match.Member.ProxyAvatar(ctx),
                match.ProxyContent, trigger.Attachments);

                        
            Task SaveMessage() => _data.AddMessage(conn, trigger.Author.Id, trigger.Channel.GuildId, trigger.Channel.Id, id, trigger.Id, match.Member.Id);
            Task LogMessage() => _logChannel.LogMessage(ctx, match, trigger, id).AsTask();
            async Task DeleteMessage()
            {
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
            
            // Run post-proxy actions (simultaneously; order doesn't matter)
            // Note that only AddMessage is using our passed-in connection, careful not to pass it elsewhere and run into conflicts
            await Task.WhenAll(
                DeleteMessage(),
                SaveMessage(),
                LogMessage()
            );
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
                await channel.SendMessageFixedAsync(
                    $"{Emojis.Error} PluralKit does not have the *Manage Webhooks* permission in this channel, and thus cannot proxy messages. Please contact a server administrator to remedy this.");
                return false;
            }

            if ((permissions & Permissions.ManageMessages) == 0)
            {
                await channel.SendMessageFixedAsync(
                    $"{Emojis.Error} PluralKit does not have the *Manage Messages* permission in this channel, and thus cannot delete the original trigger message. Please contact a server administrator to remedy this.");
                return false;
            }

            return true;
        }

        private bool CheckProxyNameBoundsOrError(string proxyName)
        {
            if (proxyName.Length < 2) throw Errors.ProxyNameTooShort(proxyName);
            if (proxyName.Length > Limits.MaxProxyNameLength) throw Errors.ProxyNameTooLong(proxyName);

            // TODO: this never returns false as it throws instead, should this happen?
            return true;
        }
    }
}