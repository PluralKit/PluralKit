using System.Text;
using System.Text.RegularExpressions;

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

namespace PluralKit.Bot;

public class ProxyService
{
    private static readonly TimeSpan MessageDeletionDelay = TimeSpan.FromMilliseconds(1000);
    private readonly IDiscordCache _cache;
    private readonly IDatabase _db;
    private readonly DispatchService _dispatch;
    private readonly LastMessageCacheService _lastMessage;

    private readonly LogChannelService _logChannel;
    private readonly ILogger _logger;
    private readonly ProxyMatcher _matcher;
    private readonly IMetrics _metrics;
    private readonly ModelRepository _repo;
    private readonly DiscordApiClient _rest;
    private readonly WebhookExecutorService _webhookExecutor;

    public ProxyService(LogChannelService logChannel, ILogger logger, WebhookExecutorService webhookExecutor,
            DispatchService dispatch, IDatabase db, ProxyMatcher matcher, IMetrics metrics, ModelRepository repo,
                                    IDiscordCache cache, DiscordApiClient rest, LastMessageCacheService lastMessage)
    {
        _logChannel = logChannel;
        _webhookExecutor = webhookExecutor;
        _dispatch = dispatch;
        _db = db;
        _matcher = matcher;
        _metrics = metrics;
        _repo = repo;
        _cache = cache;
        _lastMessage = lastMessage;
        _rest = rest;
        _logger = logger.ForContext<ProxyService>();
    }

    public async Task<bool> HandleIncomingMessage(MessageCreateEvent message, MessageContext ctx,
                                Guild guild, Channel channel, bool allowAutoproxy, PermissionSet botPermissions)
    {
        if (!ShouldProxy(channel, message, ctx))
            return false;

        var rootChannel = await _cache.GetRootChannel(message.ChannelId);

        List<ProxyMember> members;
        // Fetch members and try to match to a specific member
        using (_metrics.Measure.Timer.Time(BotMetrics.ProxyMembersQueryTime))
            members = (await _repo.GetProxyMembers(message.Author.Id, message.GuildId!.Value)).ToList();

        if (!_matcher.TryMatch(ctx, members, out var match, message.Content, message.Attachments.Length > 0,
                allowAutoproxy)) return false;

        // this is hopefully temporary, so not putting it into a separate method
        if (message.Content != null && message.Content.Length > 2000)
            throw new PKError("PluralKit cannot proxy messages over 2000 characters in length.");

        // Permission check after proxy match so we don't get spammed when not actually proxying
        if (!CheckBotPermissionsOrError(botPermissions, rootChannel.Id))
            return false;

        // this method throws, so no need to wrap it in an if statement
        CheckProxyNameBoundsOrError(match.Member.ProxyName(ctx));

        // Check if the sender account can mention everyone/here + embed links
        // we need to "mirror" these permissions when proxying to prevent exploits
        var senderPermissions = PermissionExtensions.PermissionsFor(guild, rootChannel, message);
        var allowEveryone = senderPermissions.HasFlag(PermissionSet.MentionEveryone);
        var allowEmbeds = senderPermissions.HasFlag(PermissionSet.EmbedLinks);

        // Everything's in order, we can execute the proxy!
        await ExecuteProxy(message, ctx, match, allowEveryone, allowEmbeds);
        return true;
    }

    public bool ShouldProxy(Channel channel, Message msg, MessageContext ctx)
    {
        // Make sure author has a system
        if (ctx.SystemId == null)
            throw new ProxyChecksFailedException(Errors.NoSystemError.Message);

        // Make sure channel is a guild text channel and this is a normal message
        if (!DiscordUtils.IsValidGuildChannel(channel))
            throw new ProxyChecksFailedException("This channel is not a text channel.");
        if (msg.Type != Message.MessageType.Default && msg.Type != Message.MessageType.Reply)
            throw new ProxyChecksFailedException("This message is not a normal message.");

        // Make sure author is a normal user
        if (msg.Author.System == true || msg.Author.Bot || msg.WebhookId != null)
            throw new ProxyChecksFailedException("This message was not sent by a normal user.");

        // Make sure proxying is enabled here
        if (ctx.InBlacklist)
            throw new ProxyChecksFailedException(
                "Proxying was disabled in this channel by a server administrator (via the proxy blacklist).");

        // Make sure the system has proxying enabled in the server
        if (!ctx.ProxyEnabled)
            throw new ProxyChecksFailedException(
                "Your system has proxying disabled in this server. Type `pk;proxy on` to enable it.");

        // Make sure we have either an attachment or message content
        var isMessageBlank = msg.Content == null || msg.Content.Trim().Length == 0;
        if (isMessageBlank && msg.Attachments.Length == 0)
            throw new ProxyChecksFailedException("Message cannot be blank.");

        if (msg.Activity != null)
            throw new ProxyChecksFailedException("Message contains an invite to an activity, which cannot be re-sent by PluralKit.");

        if (msg.StickerItems != null) // && msg.StickerItems.Any(s => s.Type == Sticker.StickerType.GUILD && s.GuildId != msg.GuildId))
            throw new ProxyChecksFailedException("Message contains stickers, which cannot be re-sent by PluralKit.");

        // All good!
        return true;
    }

    private async Task ExecuteProxy(Message trigger, MessageContext ctx,
                                    ProxyMatch match, bool allowEveryone, bool allowEmbeds)
    {
        // Create reply embed
        var embeds = new List<Embed>();
        if (trigger.Type == Message.MessageType.Reply && trigger.MessageReference?.ChannelId == trigger.ChannelId)
        {
            var repliedTo = trigger.ReferencedMessage.Value;
            if (repliedTo != null)
            {
                var (nickname, avatar) = await FetchReferencedMessageAuthorInfo(trigger, repliedTo);
                var embed = CreateReplyEmbed(match, trigger, repliedTo, nickname, avatar);
                if (embed != null)
                    embeds.Add(embed);
            }

            // TODO: have a clean error for when message can't be fetched instead of just being silent
        }

        // Send the webhook
        var content = match.ProxyContent;
        if (!allowEmbeds) content = content.BreakLinkEmbeds();

        var messageChannel = await _cache.GetChannel(trigger.ChannelId);
        var rootChannel = await _cache.GetRootChannel(trigger.ChannelId);
        var threadId = messageChannel.IsThread() ? messageChannel.Id : (ulong?)null;
        var guild = await _cache.GetGuild(trigger.GuildId.Value);

        var proxyMessage = await _webhookExecutor.ExecuteWebhook(new ProxyRequest
        {
            GuildId = trigger.GuildId!.Value,
            ChannelId = rootChannel.Id,
            ThreadId = threadId,
            Name = await FixSameName(messageChannel.Id, ctx, match.Member),
            AvatarUrl = AvatarUtils.TryRewriteCdnUrl(match.Member.ProxyAvatar(ctx)),
            Content = content,
            Attachments = trigger.Attachments,
            FileSizeLimit = guild.FileSizeLimit(),
            Embeds = embeds.ToArray(),
            Stickers = trigger.StickerItems,
            AllowEveryone = allowEveryone
        });
        await HandleProxyExecutedActions(ctx, trigger, proxyMessage, match);
    }

    private async Task<(string?, string?)> FetchReferencedMessageAuthorInfo(Message trigger, Message referenced)
    {
        if (referenced.WebhookId != null)
            return (null, null);

        try
        {
            var member = await _rest.GetGuildMember(trigger.GuildId!.Value, referenced.Author.Id);
            return (member?.Nick, member?.Avatar);
        }
        catch (ForbiddenException)
        {
            _logger.Warning(
                "Failed to fetch member {UserId} in guild {GuildId} when getting reply nickname, falling back to username",
                referenced.Author.Id, trigger.GuildId!.Value);
            return (null, null);
        }
    }

    private Embed CreateReplyEmbed(ProxyMatch match, Message trigger, Message repliedTo, string? nickname,
                                   string? avatar)
    {
        // repliedTo doesn't have a GuildId field :/
        var jumpLink = $"https://discord.com/channels/{trigger.GuildId}/{repliedTo.ChannelId}/{repliedTo.Id}";

        var content = new StringBuilder();

        var hasContent = !string.IsNullOrWhiteSpace(repliedTo.Content);
        if (hasContent)
        {
            var msg = repliedTo.Content;
            if (msg.Length > 100)
            {
                msg = repliedTo.Content.Substring(0, 100);
                var endsWithOpenMention = Regex.IsMatch(msg, @"<[at]?[@#:][!&]?(\w+:)?(\d+)?(:[tTdDfFR])?$");
                if (endsWithOpenMention)
                {
                    var mentionTail = repliedTo.Content.Substring(100).Split(">")[0];
                    if (repliedTo.Content.Contains(msg + mentionTail + ">"))
                        msg += mentionTail + ">";
                }

                var endsWithUrl = Regex.IsMatch(msg,
                    @"(http|https)(:\/\/)?(www\.)?([-a-zA-Z0-9@:%._\+~#=]{1,256})?\.?([a-zA-Z0-9()]{1,6})?\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$");
                if (endsWithUrl)
                {
                    var urlTail = repliedTo.Content.Substring(100).Split(" ")[0];
                    msg += urlTail + " ";
                }

                var spoilersInOriginalString = Regex.Matches(repliedTo.Content, @"\|\|").Count;
                var spoilersInTruncatedString = Regex.Matches(msg, @"\|\|").Count;
                if (spoilersInTruncatedString % 2 == 1 && spoilersInOriginalString % 2 == 0)
                    msg += "||";
                if (msg != repliedTo.Content)
                    msg += "…";
            }

            content.Append($"**[Reply to:]({jumpLink})** ");
            content.Append(msg);
            if (repliedTo.Attachments.Length > 0 || repliedTo.Embeds.Length > 0)
                content.Append($" {Emojis.Paperclip}");
        }
        else
        {
            content.Append($"*[(click to see attachment)]({jumpLink})*");
        }

        var username = nickname ?? repliedTo.Author.Username;
        var avatarUrl = avatar != null
            ? $"https://cdn.discordapp.com/guilds/{trigger.GuildId}/users/{repliedTo.Author.Id}/avatars/{avatar}.png"
            : $"https://cdn.discordapp.com/avatars/{repliedTo.Author.Id}/{repliedTo.Author.Avatar}.png";

        return new Embed
        {
            // unicodes: [three-per-em space] [left arrow emoji] [force emoji presentation]
            Author = new Embed.EmbedAuthor($"{username}\u2004\u21a9\ufe0f", IconUrl: avatarUrl),
            Description = content.ToString(),
            Color = match.Member.Color?.ToDiscordColor()
        };
    }

    private async Task<string> FixSameName(ulong channelId, MessageContext ctx, ProxyMember member)
    {
        var proxyName = member.ProxyName(ctx);

        var lastMessage = _lastMessage.GetLastMessage(channelId)?.Previous;
        if (lastMessage == null)
            // cache is out of date or channel is empty.
            return proxyName;

        var pkMessage = await _db.Execute(conn => _repo.GetMessage(conn, lastMessage.Id));

        if (lastMessage.AuthorUsername == proxyName)
        {
            // last message wasn't proxied by us, but somehow has the same name
            // it's probably from a different webhook (Tupperbox?) but let's fix it anyway!
            if (pkMessage == null)
                return FixSameNameInner(proxyName);

            // last message was proxied by a different member
            if (pkMessage.Member.Id != member.Id)
                return FixSameNameInner(proxyName);
        }

        // if we fixed the name last message and it's the same member proxying, we want to fix it again
        if (lastMessage.AuthorUsername == FixSameNameInner(proxyName) && pkMessage?.Member.Id == member.Id)
            return FixSameNameInner(proxyName);

        // No issues found, current proxy name is fine.
        return proxyName;
    }

    private string FixSameNameInner(string name)
        => $"{name}\u17b5";

    private async Task HandleProxyExecutedActions(MessageContext ctx, Message triggerMessage, Message proxyMessage, ProxyMatch match)
    {
        var sentMessage = new PKMessage
        {
            Channel = triggerMessage.ChannelId,
            Guild = triggerMessage.GuildId,
            Member = match.Member.Id,
            Mid = proxyMessage.Id,
            OriginalMid = triggerMessage.Id,
            Sender = triggerMessage.Author.Id
        };

        Task SaveMessageInDatabase()
            => _repo.AddMessage(sentMessage);

        Task LogMessageToChannel() =>
            _logChannel.LogMessage(ctx, sentMessage, triggerMessage, proxyMessage).AsTask();

        Task DispatchWebhook() => _dispatch.Dispatch(ctx.SystemId.Value, sentMessage);

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
                _logger.Debug(
                    "Trigger message {TriggerMessageId} was already deleted when we attempted to; deleting proxy message {ProxyMessageId} also",
                    triggerMessage.Id, proxyMessage.Id);
                await HandleTriggerAlreadyDeleted(proxyMessage);
                // Swallow the exception, we don't need it
            }
        }

        // Run post-proxy actions (simultaneously; order doesn't matter)
        await Task.WhenAll(
            DeleteProxyTriggerMessage(),
            SaveMessageInDatabase(),
            LogMessageToChannel(),
            DispatchWebhook()
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

    private bool CheckBotPermissionsOrError(PermissionSet permissions, ulong responseChannel)
    {
        // If we can't send messages at all, just bail immediately.
        // 2020-04-22: Manage Messages does *not* override a lack of Send Messages.
        if (!permissions.HasFlag(PermissionSet.SendMessages))
            return false;

        if (!permissions.HasFlag(PermissionSet.ManageWebhooks))
            throw new PKError("PluralKit does not have the *Manage Webhooks* permission in this channel, and thus cannot proxy messages."
                            + " Please contact a server administrator to remedy this.");

        if (!permissions.HasFlag(PermissionSet.ManageMessages))
            throw new PKError("PluralKit does not have the *Manage Messages* permission in this channel, and thus cannot delete the original trigger message."
                            + " Please contact a server administrator to remedy this.");

        return true;
    }

    private void CheckProxyNameBoundsOrError(string proxyName)
    {
        if (proxyName.Length > Limits.MaxProxyNameLength) throw Errors.ProxyNameTooLong(proxyName);
    }

    public class ProxyChecksFailedException: Exception
    {
        public ProxyChecksFailedException(string message) : base(message) { }
    }
}