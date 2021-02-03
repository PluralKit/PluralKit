using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        private readonly ModelRepository _repo;
        private readonly ILogger _logger;
        private readonly WebhookExecutorService _webhookExecutor;
        private readonly ProxyMatcher _matcher;
        private readonly IMetrics _metrics;

        public ProxyService(LogChannelService logChannel, ILogger logger,
                            WebhookExecutorService webhookExecutor, IDatabase db, ProxyMatcher matcher, IMetrics metrics, ModelRepository repo)
        {
            _logChannel = logChannel;
            _webhookExecutor = webhookExecutor;
            _db = db;
            _matcher = matcher;
            _metrics = metrics;
            _repo = repo;
            _logger = logger.ForContext<ProxyService>();
        }

        public async Task<bool> HandleIncomingMessage(DiscordClient shard, DiscordMessage message, MessageContext ctx, bool allowAutoproxy)
        {
            if (!ShouldProxy(message, ctx)) return false;

            // Fetch members and try to match to a specific member
            await using var conn = await _db.Obtain();

            List<ProxyMember> members;
            using (_metrics.Measure.Timer.Time(BotMetrics.ProxyMembersQueryTime))
                members = (await _repo.GetProxyMembers(conn, message.Author.Id, message.Channel.GuildId)).ToList();
            
            if (!_matcher.TryMatch(ctx, members, out var match, message.Content, message.Attachments.Count > 0,
                allowAutoproxy)) return false;

            // Permission check after proxy match so we don't get spammed when not actually proxying
            if (!await CheckBotPermissionsOrError(message.Channel)) return false;

            // this method throws, so no need to wrap it in an if statement
            CheckProxyNameBoundsOrError(match.Member.ProxyName(ctx));
            
            // Check if the sender account can mention everyone/here + embed links
            // we need to "mirror" these permissions when proxying to prevent exploits
            var senderPermissions = message.Channel.PermissionsInSync(message.Author);
            var allowEveryone = (senderPermissions & Permissions.MentionEveryone) != 0;
            var allowEmbeds = (senderPermissions & Permissions.EmbedLinks) != 0;

            // Everything's in order, we can execute the proxy!
            await ExecuteProxy(shard, conn, message, ctx, match, allowEveryone, allowEmbeds);
            return true;
        }

        private bool ShouldProxy(DiscordMessage msg, MessageContext ctx)
        {
            // Make sure author has a system
            if (ctx.SystemId == null) return false;
            
            // Make sure channel is a guild text channel and this is a normal message
            if ((msg.Channel.Type != ChannelType.Text && msg.Channel.Type != ChannelType.News) || msg.MessageType != MessageType.Default) return false;
            
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

        private async Task ExecuteProxy(DiscordClient shard, IPKConnection conn, DiscordMessage trigger, MessageContext ctx,
                                        ProxyMatch match, bool allowEveryone, bool allowEmbeds)
        {
            // Create reply embed
            var embeds = new List<DiscordEmbed>();
            if (trigger.Reference?.Channel?.Id == trigger.ChannelId)
            {
                var repliedTo = await FetchReplyOriginalMessage(trigger.Reference);
                if (repliedTo != null)
                {
                    var embed = CreateReplyEmbed(repliedTo);
                    if (embed != null)
                        embeds.Add(embed);
                }
                
                // TODO: have a clean error for when message can't be fetched instead of just being silent
            }
            
            // Send the webhook
            var content = match.ProxyContent;
            if (!allowEmbeds) content = content.BreakLinkEmbeds();
            var proxyMessage = await _webhookExecutor.ExecuteWebhook(trigger.Channel, FixSingleCharacterName(match.Member.ProxyName(ctx)),
                match.Member.ProxyAvatar(ctx),
                content, trigger.Attachments, embeds, allowEveryone);

            await HandleProxyExecutedActions(shard, conn, ctx, trigger, proxyMessage, match);
        }

        private async Task<DiscordMessage> FetchReplyOriginalMessage(DiscordMessageReference reference)
        {
            try
            {
                return await reference.Channel.GetMessageAsync(reference.Message.Id);
            }
            catch (NotFoundException)
            {
                _logger.Warning("Attempted to fetch reply message {ChannelId}/{MessageId} but it was not found",
                    reference.Channel.Id, reference.Message.Id);
            }
            catch (UnauthorizedException)
            {
                _logger.Warning("Attempted to fetch reply message {ChannelId}/{MessageId} but bot was not allowed to",
                    reference.Channel.Id, reference.Message.Id);
            }

            return null;
        }

        private DiscordEmbed CreateReplyEmbed(DiscordMessage original)
        {
            var content = new StringBuilder();

            var hasContent = !string.IsNullOrWhiteSpace(original.Content);
            if (hasContent)
            {
                var msg = original.Content;
                if (msg.Length > 100)
                {
                    msg = original.Content.Substring(0, 100);
                    var spoilersInOriginalString = Regex.Matches(original.Content, @"\|\|").Count;
                    var spoilersInTruncatedString = Regex.Matches(msg, @"\|\|").Count;
                    if (spoilersInTruncatedString % 2 == 1 && spoilersInOriginalString % 2 == 0)
                        msg += "||";
                    msg += "…";
                }
                
                content.Append($"**[Reply to:]({original.JumpLink})** ");
                content.Append(msg);
                if (original.Attachments.Count > 0)
                    content.Append($" {Emojis.Paperclip}");
            }
            else
            {
                content.Append($"*[(click to see attachment)]({original.JumpLink})*");
            }
            
            var username = (original.Author as DiscordMember)?.Nickname ?? original.Author.Username;
            
            return new DiscordEmbedBuilder()
                // unicodes: [three-per-em space] [left arrow emoji] [force emoji presentation]
                .WithAuthor($"{username}\u2004\u21a9\ufe0f", iconUrl: original.Author.GetAvatarUrl(ImageFormat.Png, 1024))
                .WithDescription(content.ToString())
                .Build();
        }

        private async Task HandleProxyExecutedActions(DiscordClient shard, IPKConnection conn, MessageContext ctx,
                                                      DiscordMessage triggerMessage, DiscordMessage proxyMessage,
                                                      ProxyMatch match)
        {
            Task SaveMessageInDatabase() => _repo.AddMessage(conn, new PKMessage
            {
                Channel = triggerMessage.ChannelId,
                Guild = triggerMessage.Channel.GuildId,
                Member = match.Member.Id,
                Mid = proxyMessage.Id,
                OriginalMid = triggerMessage.Id,
                Sender = triggerMessage.Author.Id
            });
            
            Task LogMessageToChannel() => _logChannel.LogMessage(shard, ctx, match, triggerMessage, proxyMessage.Id).AsTask();
            
            async Task DeleteProxyTriggerMessage()
            {
                // Wait a second or so before deleting the original message
                await Task.Delay(MessageDeletionDelay);
                try
                {
                    await triggerMessage.DeleteAsync();
                }
                catch (NotFoundException)
                {
                    _logger.Debug("Trigger message {TriggerMessageId} was already deleted when we attempted to; deleting proxy message {ProxyMessageId} also", 
                        triggerMessage.Id, proxyMessage.Id);
                    await HandleTriggerAlreadyDeleted(proxyMessage);
                    // Swallow the exception, we don't need it
                }
            }
            
            // Run post-proxy actions (simultaneously; order doesn't matter)
            // Note that only AddMessage is using our passed-in connection, careful not to pass it elsewhere and run into conflicts
            await Task.WhenAll(
                DeleteProxyTriggerMessage(),
                SaveMessageInDatabase(),
                LogMessageToChannel()
            );
        }

        private async Task HandleTriggerAlreadyDeleted(DiscordMessage proxyMessage)
        {
            // If a trigger message is deleted before we get to delete it, we can assume a mod bot or similar got to it
            // In this case we should also delete the now-proxied message.
            // This is going to hit the message delete event handler also, so that'll do the cleanup for us

            try
            {
                await proxyMessage.DeleteAsync();
            }
            catch (NotFoundException) { }
            catch (UnauthorizedException) { }
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

        private string FixSingleCharacterName(string proxyName)
        {
            if (proxyName.Length == 1) return proxyName += "\u17b5";
            else return proxyName;
        }

        private void CheckProxyNameBoundsOrError(string proxyName)
        {
            if (proxyName.Length > Limits.MaxProxyNameLength) throw Errors.ProxyNameTooLong(proxyName);
        }
    }
}