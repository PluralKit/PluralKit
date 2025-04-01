#nullable enable
using System;
using System.Text;
using System.Text.RegularExpressions;

using Autofac;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using NodaTime;

using App.Metrics;

using PluralKit.Core;
using Myriad.Gateway;
using Myriad.Utils;

namespace PluralKit.Bot;

public class ProxiedMessage
{
    private static readonly Duration EditTimeout = Duration.FromMinutes(10);
    private static readonly Duration ReproxyTimeout = Duration.FromMinutes(1);

    // private readonly IDiscordCache _cache;
    private readonly ModelRepository _repo;
    private readonly IMetrics _metrics;

    private readonly EmbedService _embeds;
    private readonly LogChannelService _logChannel;
    private readonly DiscordApiClient _rest;
    private readonly WebhookExecutorService _webhookExecutor;
    private readonly ProxyService _proxy;
    private readonly LastMessageCacheService _lastMessageCache;
    private readonly RedisService _redisService;

    public ProxiedMessage(EmbedService embeds,
                          DiscordApiClient rest, IMetrics metrics, ModelRepository repo, ProxyService proxy,
                          WebhookExecutorService webhookExecutor, LogChannelService logChannel, IDiscordCache cache,
                          LastMessageCacheService lastMessageCache, RedisService redisService)
    {
        _embeds = embeds;
        _rest = rest;
        _webhookExecutor = webhookExecutor;
        _repo = repo;
        _logChannel = logChannel;
        // _cache = cache;
        _metrics = metrics;
        _proxy = proxy;
        _lastMessageCache = lastMessageCache;
        _redisService = redisService;
    }

    public async Task ReproxyMessage(Context ctx)
    {
        var (msg, systemId) = await GetMessageToEdit(ctx, ReproxyTimeout, true);

        if (ctx.System.Id != systemId)
            throw new PKError("Can't reproxy a message sent by a different system.");

        // Get target member ID
        var target = await ctx.MatchMember(restrictToSystem: ctx.System.Id);
        if (target == null)
            throw new PKError("Could not find a member to reproxy the message with.");

        // Fetch members and get the ProxyMember for `target`
        List<ProxyMember> members;
        using (_metrics.Measure.Timer.Time(BotMetrics.ProxyMembersQueryTime))
            members = (await _repo.GetProxyMembers(ctx.Author.Id, msg.Guild!.Value)).ToList();
        var match = members.Find(x => x.Id == target.Id);
        if (match == null)
            throw new PKError("Could not find a member to reproxy the message with.");

        try
        {
            await _proxy.ExecuteReproxy(ctx.Message, msg, members, match, ctx.DefaultPrefix);

            if (ctx.Guild == null)
                await _rest.CreateReaction(ctx.Channel.Id, ctx.Message.Id, new Emoji { Name = Emojis.Success });
            if ((await ctx.BotPermissions).HasFlag(PermissionSet.ManageMessages))
                await _rest.DeleteMessage(ctx.Channel.Id, ctx.Message.Id);
        }
        catch (NotFoundException)
        {
            throw new PKError("Could not reproxy message.");
        }
    }

    public async Task EditMessage(Context ctx, bool useRegex)
    {
        var (msg, systemId) = await GetMessageToEdit(ctx, EditTimeout, false);

        if (ctx.System.Id != systemId)
            throw new PKError("Can't edit a message sent by a different system.");

        var originalMsg = await _rest.GetMessageOrNull(msg.Channel, msg.Mid);
        if (originalMsg == null)
            throw new PKError("Could not edit message.");

        // Regex flag
        useRegex = useRegex || ctx.MatchFlag("regex", "x");

        // Check if we should append or prepend
        var mutateSpace = ctx.MatchFlag("nospace", "ns") ? "" : " ";
        var append = ctx.MatchFlag("append", "a");
        var prepend = ctx.MatchFlag("prepend", "p");

        // Grab the original message content and new message content
        var originalContent = originalMsg.Content;
        var newContent = ctx.RemainderOrNull()?.NormalizeLineEndSpacing();

        // Should we clear embeds?
        var clearEmbeds = ctx.MatchFlag("clear-embed", "ce");
        var clearAttachments = ctx.MatchFlag("clear-attachments", "ca");
        if ((clearEmbeds || clearAttachments) && newContent == null)
            newContent = originalMsg.Content!;

        if (newContent == null)
            throw new PKSyntaxError("You need to include the message to edit in.");

        // Can't append or prepend a Regex
        if (useRegex && (append || prepend))
            throw new PKError("You can't use the append or prepend options with a Regex.");

        // Use the Regex to substitute the message content
        if (useRegex)
        {
            const string regexErrorStr = "Could not parse Regex. The expected formats are s|X|Y or s|X|Y|F, where | is any character, X is a valid Regex to search for matches of, Y is a substitution string, and F is a set of Regex flags.";

            // Smallest valid Regex string is "s||"; 3 chars long
            if (newContent.Length < 3 || !newContent.StartsWith('s'))
                throw new PKError(regexErrorStr);

            var separator = newContent[1];

            // s|X|Y   => ["s", "X", "Y"]
            // s|X|Y|F => ["s", "X", "Y", "F"] ("F" may be empty)
            var splitString = newContent.Split(separator);

            if (splitString.Length != 3 && splitString.Length != 4)
                throw new PKError(regexErrorStr);

            var flags = splitString.Length == 4 ? splitString[3] : "";

            var regexOptions = RegexOptions.None;
            var globalMatch = false;

            // Parse flags
            foreach (char c in flags)
            {
                switch (c)
                {
                    case 'g':
                        globalMatch = true;
                        break;

                    case 'i':
                        regexOptions |= RegexOptions.IgnoreCase;
                        break;

                    case 'm':
                        regexOptions |= RegexOptions.Multiline;
                        break;

                    case 'n':
                        regexOptions |= RegexOptions.ExplicitCapture;
                        break;

                    case 's':
                        regexOptions |= RegexOptions.Singleline;
                        break;

                    case 'x':
                        regexOptions |= RegexOptions.IgnorePatternWhitespace;
                        break;

                    default:
                        throw new PKError($"Invalid Regex flag '{c}'. Valid flags include 'g', 'i', 'm', 'n', 's', and 'x'.");
                }
            }

            try
            {
                // I would use RegexOptions.NonBacktracking but that's only .NET 7 :(
                var regex = new Regex(splitString[1], regexOptions, TimeSpan.FromSeconds(0.5));
                var numMatches = globalMatch ? -1 : 1; // Negative means all matches
                newContent = regex.Replace(originalContent!, splitString[2], numMatches);
            }
            catch (ArgumentException)
            {
                throw new PKError(regexErrorStr);
            }
            catch (RegexMatchTimeoutException)
            {
                throw new PKError("Regex took too long to run.");
            }
        }

        // Append or prepend the new content to the original message content if needed.
        // If no flag is supplied, the new contents will completely overwrite the old contents
        // If both flags are specified. the message will be prepended AND appended
        if (append && prepend)
            newContent = $"{newContent}{mutateSpace}{originalContent}{mutateSpace}{newContent}";
        else if (append)
            newContent = $"{originalContent}{mutateSpace}{newContent}";
        else if (prepend)
            newContent = $"{newContent}{mutateSpace}{originalContent}";

        if (newContent.Length > 2000)
            throw new PKError("PluralKit cannot proxy messages over 2000 characters in length.");

        // We count as an empty message even if there's an embed because the only embeds pk proxies are reply embeds and those aren't message content
        if (newContent.Trim().Length == 0 && (originalMsg.Attachments.Length == 0 || clearAttachments))
        {
            throw new PKError("This action would result in an empty message. If you wish to delete the message, react to it with \u274C.");
        }

        try
        {
            var editedMsg =
                await _webhookExecutor.EditWebhookMessage(msg.Guild ?? 0, msg.Channel, msg.Mid, newContent, clearEmbeds, clearAttachments);

            if (ctx.Guild == null)
                await _rest.CreateReaction(ctx.Channel.Id, ctx.Message.Id, new Emoji { Name = Emojis.Success });

            await _redisService.SetOriginalMid(ctx.Message.Id, editedMsg.Id);

            if ((await ctx.BotPermissions).HasFlag(PermissionSet.ManageMessages))
                await _rest.DeleteMessage(ctx.Channel.Id, ctx.Message.Id);

            await _logChannel.LogMessage(msg, ctx.Message, editedMsg, originalMsg!.Content!);
        }
        catch (NotFoundException)
        {
            throw new PKError("Could not edit message.");
        }
        catch (BadRequestException e)
        {
            if (e.Message == "Voice messages cannot be edited")
                throw new PKError($"{e.Message}.");
            throw;
        }
    }

    private async Task<(PKMessage, SystemId)> GetMessageToEdit(Context ctx, Duration timeout, bool isReproxy)
    {
        var editType = isReproxy ? "reproxy" : "edit";
        var editTypeAction = isReproxy ? "reproxied" : "edited";

        PKMessage? msg = null;

        var (referencedMessage, _) = ctx.MatchMessage(false);
        if (referencedMessage != null)
        {
            await using var conn = await ctx.Database.Obtain();
            msg = await ctx.Repository.GetMessage(referencedMessage.Value);
            if (msg == null)
                throw new PKError("This is not a message proxied by PluralKit.");
        }

        if (msg == null)
        {
            if (ctx.Guild == null)
                throw new PKSyntaxError($"You must use a message link to {editType} messages in DMs.");

            ulong? recent = null;

            if (isReproxy)
                recent = await ctx.Redis.GetLastMessage(ctx.Author.Id, ctx.Channel.Id);
            else
                recent = await FindRecentMessage(ctx, timeout);

            if (recent == null)
                throw new PKSyntaxError($"Could not find a recent message to {editType}.");

            await using var conn = await ctx.Database.Obtain();
            msg = await ctx.Repository.GetMessage(recent.Value);
            if (msg == null)
                throw new PKSyntaxError($"Could not find a recent message to {editType}.");
        }

        var member = await ctx.Repository.GetMember(msg.Member!.Value);
        if (member == null)
            throw new PKSyntaxError($"Could not find a recent message to {editType}.");

        if (msg.Channel != ctx.Channel.Id)
        {
            var error =
                "The channel where the message was sent does not exist anymore, or you are missing permissions to access it.";

            var channel = await _rest.GetChannelOrNull(msg.Channel);
            if (channel == null)
                throw new PKError(error);

            if (!await ctx.CheckPermissionsInGuildChannel(channel,
                    PermissionSet.ViewChannel | PermissionSet.SendMessages
                ))
                throw new PKError(error);
        }

        var lastMessage = await _lastMessageCache.GetLastMessage(ctx.Message.GuildId ?? 0, ctx.Message.ChannelId);

        var isLatestMessage = lastMessage?.Current.Id == ctx.Message.Id
            ? lastMessage?.Previous?.Id == msg.Mid
            : lastMessage?.Current.Id == msg.Mid;

        var msgTimestamp = DiscordUtils.SnowflakeToInstant(msg.Mid);
        if (isReproxy && !isLatestMessage)
            if (SystemClock.Instance.GetCurrentInstant() - msgTimestamp > timeout)
                throw new PKError($"The message is too old to be {editTypeAction}.");

        return (msg, member.System);
    }

    private async Task<ulong?> FindRecentMessage(Context ctx, Duration timeout)
    {
        var lastMessage = await ctx.Redis.GetLastMessage(ctx.Author.Id, ctx.Channel.Id);
        if (lastMessage == null)
            return null;

        var timestamp = DiscordUtils.SnowflakeToInstant(lastMessage.Value);
        if (SystemClock.Instance.GetCurrentInstant() - timestamp > timeout)
            return null;

        return lastMessage;
    }

    public async Task GetMessage(Context ctx)
    {
        var (messageId, _) = ctx.MatchMessage(true);
        if (messageId == null)
        {
            if (!ctx.HasNext())
                throw new PKSyntaxError("You must pass a message ID or link.");
            throw new PKSyntaxError($"Could not parse {ctx.PeekArgument().AsCode()} as a message ID or link.");
        }

        var isDelete = ctx.Match("delete") || ctx.MatchFlag("delete");

        var message = await ctx.Repository.GetFullMessage(messageId.Value);
        if (message == null)
        {
            if (isDelete)
            {
                await DeleteCommandMessage(ctx, messageId.Value);
                return;
            }

            throw Errors.MessageNotFound(messageId.Value);
        }

        var showContent = true;
        var noShowContentError = "Message deleted or inaccessible.";

        var channel = await _rest.GetChannelOrNull(message.Message.Channel);
        if (channel == null)
            showContent = false;
        else if (!await ctx.CheckPermissionsInGuildChannel(channel, PermissionSet.ViewChannel))
            showContent = false;

        var format = ctx.MatchFormat();

        if (format != ReplyFormat.Standard)
        {
            var discordMessage = await _rest.GetMessageOrNull(message.Message.Channel, message.Message.Mid);
            if (discordMessage == null || !showContent)
                throw new PKError(noShowContentError);

            var content = discordMessage.Content;
            if (content == null || content == "")
            {
                await ctx.Reply("No message content found in that message.");
                return;
            }

            if (format == ReplyFormat.Raw)
            {
                await ctx.Reply($"```{content}```");

                if (Regex.IsMatch(content, "```.*```", RegexOptions.Singleline))
                {
                    var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
                    await ctx.Rest.CreateMessage(
                        ctx.Channel.Id,
                        new MessageRequest
                        {
                            Content = $"{Emojis.Warn} Message contains codeblocks, raw source sent as an attachment."
                        },
                        new[] { new MultipartFile("message.txt", stream, null, null, null) });
                }
                return;
            }

            if (format == ReplyFormat.Plaintext)
            {
                var eb = new EmbedBuilder()
                .Description($"Showing contents of message {message.Message.Mid}");
                await ctx.Reply(content, embed: eb.Build());
                return;
            }

        }

        if (isDelete)
        {
            if (!showContent)
                throw new PKError(noShowContentError);

            // if user has has a system and their system sent the message, or if user sent the message, do not error
            if (!((ctx.System != null && message.System?.Id == ctx.System.Id) || message.Message.Sender == ctx.Author.Id))
                throw new PKError("You can only delete your own messages.");

            await ctx.Rest.DeleteMessage(message.Message.Channel, message.Message.Mid);

            if (ctx.Channel.Id == message.Message.Channel)
                await ctx.Rest.DeleteMessage(ctx.Message);
            else
                await ctx.Rest.CreateReaction(ctx.Message.ChannelId, ctx.Message.Id,
                    new Emoji { Name = Emojis.Success });

            return;
        }

        if (ctx.Match("author") || ctx.MatchFlag("author"))
        {
            var user = await _rest.GetUser(message.Message.Sender);
            var eb = new EmbedBuilder()
                .Author(new Embed.EmbedAuthor(
                    user != null
                        ? $"{user.Username}#{user.Discriminator}"
                        : $"Deleted user ${message.Message.Sender}",
                    IconUrl: user != null ? user.AvatarUrl() : null))
                .Description(message.Message.Sender.ToString());

            await ctx.Reply(
                user != null ? $"{user.Mention()} ({user.Id})" : $"*(deleted user {message.Message.Sender})*",
                eb.Build());
            return;
        }

        await ctx.Reply(embed: await _embeds.CreateMessageInfoEmbed(message, showContent, ctx.Config));
    }

    private async Task DeleteCommandMessage(Context ctx, ulong messageId)
    {
        var cmessage = await ctx.Services.Resolve<CommandMessageService>().GetCommandMessage(messageId);
        if (cmessage == null)
            throw Errors.MessageNotFound(messageId);

        if (cmessage!.AuthorId != ctx.Author.Id)
            throw new PKError("You can only delete command messages queried by this account.");

        await ctx.Rest.DeleteMessage(cmessage.ChannelId, messageId);

        if (ctx.Guild != null)
            await ctx.Rest.DeleteMessage(ctx.Message);
        else
            await ctx.Rest.CreateReaction(ctx.Message.ChannelId, ctx.Message.Id, new Emoji { Name = Emojis.Success });
    }
}