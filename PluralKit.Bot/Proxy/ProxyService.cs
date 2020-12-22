using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using App.Metrics;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

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
        private readonly IDiscordCache _cache;
        private readonly DiscordApiClient _rest;

        public ProxyService(LogChannelService logChannel, ILogger logger,
                            WebhookExecutorService webhookExecutor, IDatabase db, ProxyMatcher matcher, IMetrics metrics, ModelRepository repo, IDiscordCache cache, DiscordApiClient rest)
        {
            _logChannel = logChannel;
            _webhookExecutor = webhookExecutor;
            _db = db;
            _matcher = matcher;
            _metrics = metrics;
            _repo = repo;
            _cache = cache;
            _rest = rest;
            _logger = logger.ForContext<ProxyService>();
        }

        public async Task<bool> HandleIncomingMessage(Shard shard, MessageCreateEvent message, MessageContext ctx, Guild guild, Channel channel, bool allowAutoproxy, PermissionSet botPermissions)
        {
            if (!ShouldProxy(channel, message, ctx)) 
                return false;

            // Fetch members and try to match to a specific member
            await using var conn = await _db.Obtain();

            List<ProxyMember> members;
            using (_metrics.Measure.Timer.Time(BotMetrics.ProxyMembersQueryTime))
                members = (await _repo.GetProxyMembers(conn, message.Author.Id, message.GuildId!.Value)).ToList();
            
            if (!_matcher.TryMatch(ctx, members, out var match, message.Content, message.Attachments.Length > 0,
                allowAutoproxy)) return false;

            // Permission check after proxy match so we don't get spammed when not actually proxying
            if (!await CheckBotPermissionsOrError(botPermissions, message.ChannelId)) 
                return false;

            // this method throws, so no need to wrap it in an if statement
            CheckProxyNameBoundsOrError(match.Member.ProxyName(ctx));
            
            // Check if the sender account can mention everyone/here + embed links
            // we need to "mirror" these permissions when proxying to prevent exploits
            var senderPermissions = PermissionExtensions.PermissionsFor(guild, channel, message);
            var allowEveryone = senderPermissions.HasFlag(PermissionSet.MentionEveryone);
            var allowEmbeds = senderPermissions.HasFlag(PermissionSet.EmbedLinks);

            // Everything's in order, we can execute the proxy!
            await ExecuteProxy(shard, conn, message, ctx, match, allowEveryone, allowEmbeds);
            return true;
        }

        private bool ShouldProxy(Channel channel, Message msg, MessageContext ctx)
        {
            // Make sure author has a system
            if (ctx.SystemId == null) return false;
            
            // Make sure channel is a guild text channel and this is a normal message
            if ((channel.Type != Channel.ChannelType.GuildText && channel.Type != Channel.ChannelType.GuildNews) || msg.Type != Message.MessageType.Default) return false;
            
            // Make sure author is a normal user
            if (msg.Author.System == true || msg.Author.Bot || msg.WebhookId != null) return false;
            
            // Make sure proxying is enabled here
            if (!ctx.ProxyEnabled || ctx.InBlacklist) return false;
            
            // Make sure we have either an attachment or message content
            var isMessageBlank = msg.Content == null || msg.Content.Trim().Length == 0;
            if (isMessageBlank && msg.Attachments.Length == 0) return false;
            
            // All good!
            return true;
        }

        private async Task ExecuteProxy(Shard shard, IPKConnection conn, Message trigger, MessageContext ctx,
                                        ProxyMatch match, bool allowEveryone, bool allowEmbeds)
        {
            // Create reply embed
            var embeds = new List<Embed>();
            if (trigger.MessageReference?.ChannelId == trigger.ChannelId) 
            {
                var repliedTo = await FetchReplyOriginalMessage(trigger.MessageReference);
                var embed = CreateReplyEmbed(repliedTo);
                if (embed != null)
                    embeds.Add(embed);
            }
            
            // Send the webhook
            var content = match.ProxyContent;
            if (!allowEmbeds) content = content.BreakLinkEmbeds();

            var proxyMessage = await _webhookExecutor.ExecuteWebhook(new ProxyRequest
            {
                GuildId = trigger.GuildId!.Value,
                ChannelId = trigger.ChannelId,
                Name = FixSingleCharacterName(match.Member.ProxyName(ctx)),
                AvatarUrl = match.Member.ProxyAvatar(ctx),
                Content = content,
                Attachments = trigger.Attachments,
                Embeds = embeds.ToArray(),
                AllowEveryone = allowEveryone,
            });
            await HandleProxyExecutedActions(shard, conn, ctx, trigger, proxyMessage, match);
        }

        private async Task<Message?> FetchReplyOriginalMessage(Message.Reference reference)
        {
            try
            {
                var msg = await _rest.GetMessage(reference.ChannelId!.Value, reference.MessageId!.Value);
                if (msg == null)
                    _logger.Warning("Attempted to fetch reply message {ChannelId}/{MessageId} but it was not found",
                        reference.ChannelId, reference.MessageId);
                return msg;
            }
            catch (UnauthorizedException)
            {
                _logger.Warning("Attempted to fetch reply message {ChannelId}/{MessageId} but bot was not allowed to",
                    reference.ChannelId, reference.MessageId);
            }

            return null;
        }

        private Embed CreateReplyEmbed(Message original)
        {
            var jumpLink = $"https://discord.com/channels/{original.GuildId}/{original.ChannelId}/{original.Id}";
            
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
                
                content.Append($"**[Reply to:]({jumpLink})** ");
                content.Append(msg);
                if (original.Attachments.Length > 0)
                    content.Append($" {Emojis.Paperclip}");
            }
            else
            {
                content.Append($"*[(click to see attachment)]({jumpLink})*");
            }
            
            // TODO: get the nickname somehow
            var username = original.Author.Username;
            // var username = original.Member?.Nick ?? original.Author.Username;

            var avatarUrl = $"https://cdn.discordapp.com/avatars/{original.Author.Id}/{original.Author.Avatar}.png";

            return new Embed
            {
                // unicodes: [three-per-em space] [left arrow emoji] [force emoji presentation]
                Author = new($"{username}\u2004\u21a9\ufe0f", IconUrl: avatarUrl),
                Description = content.ToString()
            };
        }

        private async Task HandleProxyExecutedActions(Shard shard, IPKConnection conn, MessageContext ctx,
                                                      Message triggerMessage, Message proxyMessage,
                                                      ProxyMatch match)
        {
            Task SaveMessageInDatabase() => _repo.AddMessage(conn, new PKMessage
            {
                Channel = triggerMessage.ChannelId,
                Guild = triggerMessage.GuildId,
                Member = match.Member.Id,
                Mid = proxyMessage.Id,
                OriginalMid = triggerMessage.Id,
                Sender = triggerMessage.Author.Id
            });
            
            Task LogMessageToChannel() => _logChannel.LogMessage(ctx, match, triggerMessage, proxyMessage.Id).AsTask();
            
            async Task DeleteProxyTriggerMessage()
            {
                // Wait a second or so before deleting the original message
                await Task.Delay(MessageDeletionDelay);
                try
                {
                    await _rest.DeleteMessage(triggerMessage.ChannelId, triggerMessage.Id);
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

        private async Task HandleTriggerAlreadyDeleted(Message proxyMessage)
        {
            // If a trigger message is deleted before we get to delete it, we can assume a mod bot or similar got to it
            // In this case we should also delete the now-proxied message.
            // This is going to hit the message delete event handler also, so that'll do the cleanup for us

            try
            {
                await _rest.DeleteMessage(proxyMessage.ChannelId, proxyMessage.Id);
            }
            catch (NotFoundException) { }
            catch (UnauthorizedException) { }
        }

        private async Task<bool> CheckBotPermissionsOrError(PermissionSet permissions, ulong responseChannel)
        {
            // If we can't send messages at all, just bail immediately.
            // 2020-04-22: Manage Messages does *not* override a lack of Send Messages.
            if (!permissions.HasFlag(PermissionSet.SendMessages)) 
                return false;

            if (!permissions.HasFlag(PermissionSet.ManageWebhooks))
            {
                // todo: PKError-ify these
                await _rest.CreateMessage(responseChannel, new MessageRequest
                {
                    Content = $"{Emojis.Error} PluralKit does not have the *Manage Webhooks* permission in this channel, and thus cannot proxy messages. Please contact a server administrator to remedy this."
                });
                return false;
            }

            if (!permissions.HasFlag(PermissionSet.ManageMessages))
            {
                await _rest.CreateMessage(responseChannel, new MessageRequest
                {
                    Content = $"{Emojis.Error} PluralKit does not have the *Manage Messages* permission in this channel, and thus cannot delete the original trigger message. Please contact a server administrator to remedy this."
                });
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