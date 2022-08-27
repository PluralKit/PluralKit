#nullable enable
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

    public ProxiedMessage(EmbedService embeds,
                          DiscordApiClient rest, IMetrics metrics, ModelRepository repo, ProxyService proxy,
                          WebhookExecutorService webhookExecutor, LogChannelService logChannel, IDiscordCache cache)
    {
        _embeds = embeds;
        _rest = rest;
        _webhookExecutor = webhookExecutor;
        _repo = repo;
        _logChannel = logChannel;
        // _cache = cache;
        _metrics = metrics;
        _proxy = proxy;
    }

    public async Task ReproxyMessage(Context ctx)
    {
        var msg = await GetMessageToEdit(ctx, ReproxyTimeout, true);

        if (ctx.System.Id != msg.System?.Id)
            throw new PKError("Can't reproxy a message sent by a different system.");

        // Get target member ID
        var target = await ctx.MatchMember(restrictToSystem: ctx.System.Id);
        if (target == null)
            throw new PKError("Could not find a member to reproxy the message with.");

        // Fetch members and get the ProxyMember for `target`
        List<ProxyMember> members;
        using (_metrics.Measure.Timer.Time(BotMetrics.ProxyMembersQueryTime))
            members = (await _repo.GetProxyMembers(ctx.Author.Id, msg.Message.Guild!.Value)).ToList();
        var match = members.Find(x => x.Id == target.Id);
        if (match == null)
            throw new PKError("Could not find a member to reproxy the message with.");

        try
        {
            await _proxy.ExecuteReproxy(ctx.Message, msg.Message, members, match);

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

    public async Task EditMessage(Context ctx)
    {
        var msg = await GetMessageToEdit(ctx, EditTimeout, false);

        if (ctx.System.Id != msg.System?.Id)
            throw new PKError("Can't edit a message sent by a different system.");

        if (!ctx.HasNext())
            throw new PKSyntaxError("You need to include the message to edit in.");

        var originalMsg = await _rest.GetMessageOrNull(msg.Message.Channel, msg.Message.Mid);
        if (originalMsg == null)
            throw new PKError("Could not edit message.");

        // Check if we should append or prepend
        var append = ctx.MatchFlag("append");
        var prepend = ctx.MatchFlag("prepend");

        // Grab the original message content and new message content
        var originalContent = originalMsg.Content;
        var newContent = ctx.RemainderOrNull().NormalizeLineEndSpacing();

        // Append or prepend the new content to the original message content if needed.
        // If no flag is supplied, the new contents will completly overwrite the old contents
        // If both flags are specified. the message will be prepended AND appended
        if (append && prepend) newContent = $"{newContent} {originalContent} {newContent}";
        else if (append) newContent = originalContent + " " + newContent;
        else if (prepend) newContent = newContent + " " + originalContent;

        if (newContent.Length > 2000)
            throw new PKError("PluralKit cannot proxy messages over 2000 characters in length.");

        try
        {
            var editedMsg =
                await _webhookExecutor.EditWebhookMessage(msg.Message.Channel, msg.Message.Mid, newContent);

            if (ctx.Guild == null)
                await _rest.CreateReaction(ctx.Channel.Id, ctx.Message.Id, new Emoji { Name = Emojis.Success });

            if ((await ctx.BotPermissions).HasFlag(PermissionSet.ManageMessages))
                await _rest.DeleteMessage(ctx.Channel.Id, ctx.Message.Id);

            await _logChannel.LogMessage(msg.Message, ctx.Message, editedMsg, originalMsg!.Content!);
        }
        catch (NotFoundException)
        {
            throw new PKError("Could not edit message.");
        }
    }

    private async Task<FullMessage> GetMessageToEdit(Context ctx, Duration timeout, bool isReproxy)
    {
        var editType = isReproxy ? "reproxy" : "edit";
        var editTypeAction = isReproxy ? "reproxied" : "edited";

        FullMessage? msg = null;

        var (referencedMessage, _) = ctx.MatchMessage(false);
        if (referencedMessage != null)
        {
            await using var conn = await ctx.Database.Obtain();
            msg = await ctx.Repository.GetMessage(conn, referencedMessage.Value);
            if (msg == null)
                throw new PKError("This is not a message proxied by PluralKit.");
        }

        if (msg == null)
        {
            if (ctx.Guild == null)
                throw new PKSyntaxError($"You must use a message link to {editType} messages in DMs.");

            PKMessage? recent;

            if (isReproxy)
                recent = await ctx.Repository.GetLastMessage(ctx.Guild.Id, ctx.Channel.Id, ctx.Author.Id);
            else
                recent = await FindRecentMessage(ctx, timeout);

            if (recent == null)
                throw new PKSyntaxError($"Could not find a recent message to {editType}.");

            await using var conn = await ctx.Database.Obtain();
            msg = await ctx.Repository.GetMessage(conn, recent.Mid);
            if (msg == null)
                throw new PKSyntaxError($"Could not find a recent message to {editType}.");
        }

        if (msg.Message.Channel != ctx.Channel.Id)
        {
            var error =
                "The channel where the message was sent does not exist anymore, or you are missing permissions to access it.";

            var channel = await _rest.GetChannelOrNull(msg.Message.Channel);
            if (channel == null)
                throw new PKError(error);

            if (!await ctx.CheckPermissionsInGuildChannel(channel,
                    PermissionSet.ViewChannel | PermissionSet.SendMessages
                ))
                throw new PKError(error);
        }

        var latestMessages = await _rest.GetChannelMessages(msg.Message.Channel, 2);
        var isLatestMessage = latestMessages.LastOrDefault()?.Id == ctx.Message.Id
            ? latestMessages.FirstOrDefault()?.Id == msg.Message.Mid
            : latestMessages.LastOrDefault()?.Id == msg.Message.Mid;

        var msgTimestamp = DiscordUtils.SnowflakeToInstant(msg.Message.Mid);
        if (isReproxy && !isLatestMessage)
            if (SystemClock.Instance.GetCurrentInstant() - msgTimestamp > timeout)
                throw new PKError($"The message is too old to be {editTypeAction}.");

        return msg;
    }

    private async Task<PKMessage?> FindRecentMessage(Context ctx, Duration timeout)
    {
        var lastMessage = await ctx.Repository.GetLastMessage(ctx.Guild.Id, ctx.Channel.Id, ctx.Author.Id);
        if (lastMessage == null)
            return null;

        var timestamp = DiscordUtils.SnowflakeToInstant(lastMessage.Mid);
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

        var message = await ctx.Database.Execute(c => ctx.Repository.GetMessage(c, messageId.Value));
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

        if (ctx.MatchRaw())
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
                    new[] { new MultipartFile("message.txt", stream, null) });
            }

            return;
        }

        if (isDelete)
        {
            if (!showContent)
                throw new PKError(noShowContentError);

            if (message.System?.Id != ctx.System.Id && message.Message.Sender != ctx.Author.Id)
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

        await ctx.Reply(embed: await _embeds.CreateMessageInfoEmbed(message, showContent));
    }

    private async Task DeleteCommandMessage(Context ctx, ulong messageId)
    {
        var (authorId, channelId) = await ctx.Services.Resolve<CommandMessageService>().GetCommandMessage(messageId);
        if (authorId == null)
            throw Errors.MessageNotFound(messageId);

        if (authorId != ctx.Author.Id)
            throw new PKError("You can only delete command messages queried by this account.");

        await ctx.Rest.DeleteMessage(channelId!.Value, messageId);

        if (ctx.Guild != null)
            await ctx.Rest.DeleteMessage(ctx.Message);
        else
            await ctx.Rest.CreateReaction(ctx.Message.ChannelId, ctx.Message.Id, new Emoji { Name = Emojis.Success });
    }
}