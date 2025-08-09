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
    private readonly RedisService _redis;
    private readonly DispatchService _dispatch;
    private readonly LastMessageCacheService _lastMessage;

    private readonly LogChannelService _logChannel;
    private readonly ILogger _logger;
    private readonly ProxyMatcher _matcher;
    private readonly IMetrics _metrics;
    private readonly ModelRepository _repo;
    private readonly DiscordApiClient _rest;
    private readonly WebhookExecutorService _webhookExecutor;
    private readonly NodaTime.IClock _clock;

    private static readonly string URL_REGEX = @"(http|https)(:\/\/)?(www\.)?([-a-zA-Z0-9@:%._\+~#=]{1,256})?\.?([a-zA-Z0-9()]{1,6})?\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)$";

    public ProxyService(LogChannelService logChannel, ILogger logger, WebhookExecutorService webhookExecutor,
            DispatchService dispatch, IDatabase db, RedisService redis, ProxyMatcher matcher, IMetrics metrics, ModelRepository repo,
                      NodaTime.IClock clock, IDiscordCache cache, DiscordApiClient rest, LastMessageCacheService lastMessage)
    {
        _logChannel = logChannel;
        _webhookExecutor = webhookExecutor;
        _dispatch = dispatch;
        _db = db;
        _redis = redis;
        _matcher = matcher;
        _metrics = metrics;
        _repo = repo;
        _cache = cache;
        _lastMessage = lastMessage;
        _rest = rest;
        _clock = clock;
        _logger = logger.ForContext<ProxyService>();
    }

    public async Task<bool> HandleIncomingMessage(MessageCreateEvent message, MessageContext ctx,
                                Guild guild, Channel channel, bool allowAutoproxy, PermissionSet botPermissions, string prefix)
    {
        var rootChannel = await _cache.GetRootChannel(message.GuildId!.Value, message.ChannelId);

        if (!ShouldProxy(channel, rootChannel, message, ctx, prefix))
            return false;

        var autoproxySettings = await _repo.GetAutoproxySettings(ctx.SystemId.Value, guild.Id, null);

        if (autoproxySettings.AutoproxyMode == AutoproxyMode.Latch && IsUnlatch(message))
        {
            // "unlatch"
            await _repo.UpdateAutoproxy(ctx.SystemId.Value, guild.Id, null, new()
            {
                AutoproxyMember = null
            });
            return false;
        }

        List<ProxyMember> members;
        // Fetch members and try to match to a specific member
        using (_metrics.Measure.Timer.Time(BotMetrics.ProxyMembersQueryTime))
            members = (await _repo.GetProxyMembers(message.Author.Id, message.GuildId!.Value)).ToList();

        if (!_matcher.TryMatch(ctx, autoproxySettings, members, out var match, message.Content, prefix, message.Attachments.Length > 0,
                allowAutoproxy, ctx.CaseSensitiveProxyTags)) return false;

        var canProxy = await CanProxy(channel, rootChannel, message, ctx, prefix);
        if (canProxy != null)
        {
            if (ctx.ProxyErrorMessageEnabled)
                throw new PKError(canProxy);

            return false;
        }

        // Permission check after proxy match so we don't get spammed when not actually proxying
        if (!CheckBotPermissionsOrError(botPermissions, rootChannel.Id))
            return false;

        // this method throws, so no need to wrap it in an if statement
        CheckProxyNameBoundsOrError(match.Member.ProxyName(ctx));

        // Check if the sender account can mention everyone/here + embed links
        // we need to "mirror" these permissions when proxying to prevent exploits
        var senderPermissions = PermissionExtensions.PermissionsFor(guild, rootChannel, message.Author.Id, message.Member, isThread: rootChannel.Id != channel.Id);
        var allowEveryone = senderPermissions.HasFlag(PermissionSet.MentionEveryone);
        var allowEmbeds = senderPermissions.HasFlag(PermissionSet.EmbedLinks);

        // Everything's in order, we can execute the proxy!
        await ExecuteProxy(message, ctx, autoproxySettings, match, allowEveryone, allowEmbeds);
        return true;
    }

    // Proxy checks that give user errors
    public async Task<string> CanProxy(Channel channel, Channel rootChannel, Message msg, MessageContext ctx, string prefix)
    {
        if (!DiscordUtils.IsValidGuildChannel(channel))
            return $"PluralKit cannot proxy messages in this type of channel.";

        // Check if the message does not go over any Discord Nitro limits
        if (msg.Content != null && msg.Content.Length > 2000)
        {
            return "PluralKit cannot proxy messages over 2000 characters in length.";
        }

        if (ctx.RequireSystemTag)
        {
            if (!ctx.TagEnabled)
            {
                return "This server requires PluralKit users to have a system tag, but your system tag is disabled in this server. " +
                    $"Use `{prefix}s servertag -enable` to enable it for this server.";
            }

            if (!ctx.HasProxyableTag())
            {
                return "This server requires PluralKit users to have a system tag, but you do not have one set. " +
                    $"A system tag can be set for all servers with `{prefix}s tag`, or for just this server with `{prefix}s servertag`.";
            }
        }

        var guild = await _cache.GetGuild(channel.GuildId.Value);
        var fileSizeLimit = guild.FileSizeLimit();
        var bytesThreshold = fileSizeLimit * 1024 * 1024;

        foreach (var attachment in msg.Attachments)
        {
            if (attachment.Size > bytesThreshold)
            {
                return $"PluralKit cannot proxy attachments over {fileSizeLimit} megabytes in this server (as webhooks aren't considered as having Discord Nitro) :(";
            }
        }

        return null;
    }

    // Proxy checks that don't give user errors unless `pk;debug proxy` is used
    public bool ShouldProxy(Channel channel, Channel rootChannel, Message msg, MessageContext ctx, string prefix)
    {
        // Make sure author has a system
        if (ctx.SystemId == null)
            throw new ProxyChecksFailedException(Errors.NoSystemError(prefix).Message);

        // Make sure channel is a guild text channel and this is a normal message
        if (!DiscordUtils.IsValidGuildChannel(channel))
            throw new ProxyChecksFailedException("This channel is not a text channel.");
        if (msg.Type != Message.MessageType.Default && msg.Type != Message.MessageType.Reply)
            throw new ProxyChecksFailedException("This message is not a normal message.");

        // Make sure author is a normal user
        if (msg.Author.System == true || msg.Author.Bot || msg.WebhookId != null)
            throw new ProxyChecksFailedException("This message was not sent by a normal user.");

        // Make sure this message does not start a forum thread
        if (msg.Id == msg.ChannelId)
            throw new ProxyChecksFailedException("This message is the initial message in a forum post, which PluralKit is unable to proxy correctly.");

        // Make sure proxying is enabled here
        if (ctx.InBlacklist)
            throw new ProxyChecksFailedException(
                "Proxying was disabled in this channel by a server administrator (via the proxy blacklist).");

        // Make sure the system has proxying enabled in the server
        if (!ctx.ProxyEnabled)
            throw new ProxyChecksFailedException(
                $"Your system has proxying disabled in this server. Type `{prefix}proxy on` to enable it.");

        // Make sure we have an attachment, message content, or poll
        var isMessageBlank = msg.Content == null || msg.Content.Trim().Length == 0;
        if (isMessageBlank && msg.Attachments.Length == 0 && msg.Poll == null)
            throw new ProxyChecksFailedException("Message cannot be blank.");

        if (msg.Activity != null)
            throw new ProxyChecksFailedException("Message contains an invite to an activity, which cannot be re-sent by PluralKit.");

        if (msg.StickerItems != null) // && msg.StickerItems.Any(s => s.Type == Sticker.StickerType.GUILD && s.GuildId != msg.GuildId))
            throw new ProxyChecksFailedException("Message contains stickers, which cannot be re-sent by PluralKit.");

        // All good!
        return true;
    }

    private async Task ExecuteProxy(Message trigger, MessageContext ctx, AutoproxySettings autoproxySettings,
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

        var messageChannel = await _cache.GetChannel(trigger.GuildId!.Value, trigger.ChannelId);
        var rootChannel = await _cache.GetRootChannel(trigger.GuildId!.Value, trigger.ChannelId);
        var threadId = messageChannel.IsThread() ? messageChannel.Id : (ulong?)null;
        var guild = await _cache.GetGuild(trigger.GuildId.Value);
        var guildMember = await _rest.GetGuildMember(trigger.GuildId!.Value, trigger.Author.Id);

        //If the member is a text-to-speech member and the user can send text-to-speech messages in that channel, turn text-to-speech on
        var senderPermissions = PermissionExtensions.PermissionsFor(guild, messageChannel, trigger.Author.Id, guildMember);
        var tts = match.Member.Tts && senderPermissions.HasFlag(PermissionSet.SendTtsMessages);

        Message.MessageFlags flags = 0;
        if (ctx.SuppressNotifications)
            flags |= Message.MessageFlags.SuppressNotifications;
        if (trigger.Flags.HasFlag(Message.MessageFlags.VoiceMessage))
            flags |= Message.MessageFlags.VoiceMessage;

        try
        {
            var proxyMessage = await _webhookExecutor.ExecuteWebhook(new ProxyRequest
            {
                GuildId = trigger.GuildId!.Value,
                ChannelId = rootChannel.Id,
                ThreadId = threadId,
                MessageId = trigger.Id,
                Name = await FixSameName(trigger.GuildId!.Value, messageChannel.Id, ctx, match.Member),
                AvatarUrl = AvatarUtils.TryRewriteCdnUrl(match.Member.ProxyAvatar(ctx)),
                Content = content,
                Attachments = trigger.Attachments,
                FileSizeLimit = guild.FileSizeLimit(),
                Embeds = embeds.ToArray(),
                Stickers = trigger.StickerItems,
                AllowEveryone = allowEveryone,
                Flags = flags,
                Tts = tts,
                Poll = trigger.Poll,
            });
            await HandleProxyExecutedActions(ctx, autoproxySettings, trigger, proxyMessage, match);
        }
        catch (PKError)
        {
            if (ctx.ProxyErrorMessageEnabled)
                throw;
        }
    }

    public async Task ExecuteReproxy(Message trigger, PKMessage msg, List<ProxyMember> members, ProxyMember member, string prefix)
    {
        var originalMsg = await _rest.GetMessageOrNull(msg.Channel, msg.Mid);
        if (originalMsg == null)
            throw new PKError("Could not reproxy message.");

        var messageChannel = await _rest.GetChannelOrNull(msg.Channel!);
        var rootChannel = messageChannel.IsThread() ? await _rest.GetChannelOrNull(messageChannel.ParentId!.Value) : messageChannel;

        // Get a MessageContext for the original message
        MessageContext ctx =
            await _repo.GetMessageContext(msg.Sender, msg.Guild!.Value, rootChannel.Id, msg.Channel);

        // Make sure proxying is enabled here
        if (ctx.InBlacklist)
            throw new ProxyChecksFailedException(
                "Proxying was disabled in this channel by a server administrator (via the proxy blacklist).");

        var autoproxySettings = await _repo.GetAutoproxySettings(ctx.SystemId.Value, msg.Guild!.Value, null);
        var config = await _repo.GetSystemConfig(ctx.SystemId.Value);
        var prevMatched = _matcher.TryMatch(ctx, autoproxySettings, members, out var prevMatch, originalMsg.Content, prefix,
                                            originalMsg.Attachments.Length > 0, false, ctx.CaseSensitiveProxyTags);

        var match = new ProxyMatch
        {
            Member = member,
            Content = prevMatched ? prevMatch.Content : originalMsg.Content,
            ProxyTags = member.ProxyTags.FirstOrDefault(),
        };

        var threadId = messageChannel.IsThread() ? messageChannel.Id : (ulong?)null;
        var guild = await _rest.GetGuildOrNull(msg.Guild!.Value);
        var guildMember = await _rest.GetGuildMember(msg.Guild!.Value, trigger.Author.Id);

        // Grab user permissions
        var senderPermissions = PermissionExtensions.PermissionsFor(guild, messageChannel, trigger.Author.Id, guildMember);
        var allowEveryone = senderPermissions.HasFlag(PermissionSet.MentionEveryone);

        // Make sure user has permissions to send messages
        if (!senderPermissions.HasFlag(PermissionSet.SendMessages))
            throw new PKError("You don't have permission to send messages in the channel that message is in.");

        //If the member is a text-to-speech member and the user can send text-to-speech messages in that channel, turn text-to-speech on
        var tts = member.Tts && senderPermissions.HasFlag(PermissionSet.SendTtsMessages);

        // Mangle embeds (for reply embed color changing)
        var mangledEmbeds = originalMsg.Embeds!.Select(embed => MangleReproxyEmbed(embed, member)).Where(embed => embed != null).ToArray();

        // Send the reproxied webhook
        var proxyMessage = await _webhookExecutor.ExecuteWebhook(new ProxyRequest
        {
            GuildId = guild.Id,
            ChannelId = rootChannel.Id,
            ThreadId = threadId,
            MessageId = originalMsg.Id,
            Name = match.Member.ProxyName(ctx),
            AvatarUrl = AvatarUtils.TryRewriteCdnUrl(match.Member.ProxyAvatar(ctx)),
            Content = match.ProxyContent!,
            Attachments = originalMsg.Attachments!,
            FileSizeLimit = guild.FileSizeLimit(),
            Embeds = mangledEmbeds,
            Stickers = originalMsg.StickerItems!,
            AllowEveryone = allowEveryone,
            Flags = originalMsg.Flags.HasFlag(Message.MessageFlags.VoiceMessage) ? Message.MessageFlags.VoiceMessage : null,
            Tts = tts,
            Poll = originalMsg.Poll,
        });


        await HandleProxyExecutedActions(ctx, autoproxySettings, trigger, proxyMessage, match, deletePrevious: false);
        await _rest.DeleteMessage(originalMsg.ChannelId!, originalMsg.Id!);
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

    private Embed? MangleReproxyEmbed(Embed embed, ProxyMember member)
    {
        // XXX: This is a naïve implementation of detecting reply embeds: looking for the same Unicode
        // characters as used in the reply embed generation, since we don't _really_ have a good way
        // to detect whether an embed is a PluralKit reply embed right now, whether a message is in
        // reply to another message isn't currently stored anywhere in the database.
        //
        // unicodes: [three-per-em space] [left arrow emoji] [force emoji presentation]
        if (embed.Author != null && embed.Author!.Name.EndsWith("\u2004\u21a9\ufe0f"))
        {
            return new Embed
            {
                Type = "rich",
                Author = embed.Author!,
                Description = embed.Description!,
                Color = member.Color?.ToDiscordColor()
            };
        }

        // XXX: remove non-rich embeds as including them breaks link embeds completely
        else if (embed.Type != "rich")
        {
            return null;
        }

        return embed;
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

            // strip out overly excessive line breaks
            msg = Regex.Replace(msg, @"(?:(?:([_\*]) \1)?\n){2,}", "\n");

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

                var endsWithUrl = Regex.IsMatch(msg, URL_REGEX);
                if (endsWithUrl)
                {
                    msg += repliedTo.Content.Substring(100).Split(" ")[0];

                    // replace the entire URL with a placeholder if it's *too* long
                    if (msg.Length > 300)
                        msg = Regex.Replace(msg, URL_REGEX, $"*[(very long link removed, click to see original message)]({jumpLink})*");

                    msg += " ";
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

        var username = nickname ?? repliedTo.Author.GlobalName ?? repliedTo.Author.Username;
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

    private async Task<string> FixSameName(ulong guildId, ulong channelId, MessageContext ctx, ProxyMember member)
    {
        var proxyName = member.ProxyName(ctx);

        var lastMessage = (await _lastMessage.GetLastMessage(guildId, channelId))?.Previous;
        if (lastMessage == null)
            // cache is out of date or channel is empty.
            return proxyName;

        var pkMessage = await _repo.GetMessage(lastMessage.Id);

        if (lastMessage.AuthorUsername == proxyName)
        {
            // last message wasn't proxied by us, but somehow has the same name
            // it's probably from a different webhook (Tupperbox?) but let's fix it anyway!
            if (pkMessage == null)
                return FixSameNameInner(proxyName);

            // last message was proxied by a different member
            if (pkMessage.Member != member.Id)
                return FixSameNameInner(proxyName);
        }

        // if we fixed the name last message and it's the same member proxying, we want to fix it again
        if (lastMessage.AuthorUsername == FixSameNameInner(proxyName) && pkMessage?.Member == member.Id)
            return FixSameNameInner(proxyName);

        // No issues found, current proxy name is fine.
        return proxyName;
    }

    private string FixSameNameInner(string name)
        => $"{name}\u200a\u17b5";

    public static bool IsUnlatch(Message message)
        => message.Content.StartsWith(@"\\") || message.Content.StartsWith("\\\u200b\\");

    private async Task HandleProxyExecutedActions(MessageContext ctx, AutoproxySettings autoproxySettings,
                                                  Message triggerMessage, Message proxyMessage, ProxyMatch match,
                                                  bool deletePrevious = true)
    {
        var sentMessage = new PKMessage
        {
            Channel = proxyMessage.ChannelId,
            Guild = proxyMessage.GuildId,
            Member = match.Member.Id,
            Mid = proxyMessage.Id,
            OriginalMid = triggerMessage.Id,
            Sender = triggerMessage.Author.Id
        };

        Task saveMessageInDatabase = _repo.AddMessage(sentMessage);

        async Task SaveMessageInRedis()
        {
            // logclean info
            await _redis.SetLogCleanup(triggerMessage.Author.Id, proxyMessage.GuildId!.Value);

            // last message info (edit/reproxy)
            await _redis.SetLastMessage(triggerMessage.Author.Id, proxyMessage.ChannelId, sentMessage.Mid);

            // "by original mid" lookup
            await _redis.SetOriginalMid(triggerMessage.Id, proxyMessage.Id);
        }

        Task UpdateMemberForSentMessage()
            => _repo.UpdateMemberForSentMessage(sentMessage.Member!.Value);

        Task LogMessageToChannel() =>
            _logChannel.LogMessage(sentMessage, triggerMessage, proxyMessage).AsTask();

        Task SaveLatchAutoproxy() => autoproxySettings.AutoproxyMode == AutoproxyMode.Latch
            ? _repo.UpdateAutoproxy(ctx.SystemId.Value, triggerMessage.GuildId, null, new()
            {
                AutoproxyMember = match.Member.Id,
                LastLatchTimestamp = _clock.GetCurrentInstant(),
            })
            : Task.CompletedTask;

        Task DispatchWebhook() => _dispatch.Dispatch(ctx.SystemId.Value, sentMessage);

        async Task MaybeLogSwitch()
        {
            if (ctx.ProxySwitch == SystemConfig.ProxySwitchAction.New && !Array.Exists(ctx.LastSwitchMembers, element => element == match.Member.Id))
                await _db.Execute(conn => _repo.AddSwitch(conn, (SystemId)ctx.SystemId, new[] { match.Member.Id }));
            else if (ctx.ProxySwitch == SystemConfig.ProxySwitchAction.Add)
            {
                if (ctx.LastSwitchMembers.Length == 0)
                {
                    await _db.Execute(conn => _repo.AddSwitch(conn, (SystemId)ctx.SystemId, new[] { match.Member.Id }));
                }
                else if (!Array.Exists(ctx.LastSwitchMembers, element => element == match.Member.Id))
                {
                    await _db.Execute(conn => _repo.EditSwitch(conn, (SwitchId)ctx.LastSwitch, ctx.LastSwitchMembers.Append(match.Member.Id).ToList()));
                }
            }
        }

        async Task DeleteProxyTriggerMessage()
        {
            if (!deletePrevious)
                return;

            // Wait a second or so before deleting the original message
            await Task.Delay(MessageDeletionDelay);

            // Wait until the message info is done saving in the database
            await saveMessageInDatabase;

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
            saveMessageInDatabase,
            SaveMessageInRedis(),
            UpdateMemberForSentMessage(),
            LogMessageToChannel(),
            SaveLatchAutoproxy(),
            DispatchWebhook(),
            MaybeLogSwitch()
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