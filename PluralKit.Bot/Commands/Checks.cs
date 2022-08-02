using Humanizer;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Checks
{
    private readonly BotConfig _botConfig;
    // this must ONLY be used to get the bot's user ID
    private readonly IDiscordCache _cache;
    private readonly ProxyMatcher _matcher;
    private readonly ProxyService _proxy;
    private readonly DiscordApiClient _rest;

    private readonly PermissionSet[] requiredPermissions =
    {
        PermissionSet.ViewChannel, PermissionSet.SendMessages, PermissionSet.AddReactions,
        PermissionSet.AttachFiles, PermissionSet.EmbedLinks, PermissionSet.ManageMessages,
        PermissionSet.ManageWebhooks, PermissionSet.ReadMessageHistory
    };

    // todo: make sure everything uses the minimum amount of REST calls necessary
    public Checks(DiscordApiClient rest, IDiscordCache cache, BotConfig botConfig, ProxyService proxy, ProxyMatcher matcher)
    {
        _rest = rest;
        _cache = cache;
        _botConfig = botConfig;
        _proxy = proxy;
        _matcher = matcher;
    }

    public async Task PermCheckGuild(Context ctx)
    {
        Guild guild;
        GuildMemberPartial senderGuildUser = null;

        if (ctx.Guild != null && !ctx.HasNext())
        {
            guild = ctx.Guild;
            senderGuildUser = ctx.Member;
        }
        else
        {
            var guildIdStr = ctx.RemainderOrNull() ??
                             throw new PKSyntaxError("You must pass a server ID or run this command in a server.");
            if (!ulong.TryParse(guildIdStr, out var guildId))
                throw new PKSyntaxError($"Could not parse {guildIdStr.AsCode()} as an ID.");

            try
            {
                guild = await _rest.GetGuild(guildId);
            }
            catch (ForbiddenException)
            {
                throw Errors.GuildNotFound(guildId);
            }

            if (guild != null)
                senderGuildUser = await _rest.GetGuildMember(guildId, ctx.Author.Id);
            if (guild == null || senderGuildUser == null)
                throw Errors.GuildNotFound(guildId);
        }

        var guildMember = await _rest.GetGuildMember(guild.Id, await _cache.GetOwnUser());

        // Loop through every channel and group them by sets of permissions missing
        var permissionsMissing = new Dictionary<ulong, List<Channel>>();
        var hiddenChannels = false;
        var missingEmojiPermissions = false;
        foreach (var channel in await _rest.GetGuildChannels(guild.Id))
        {
            var botPermissions = PermissionExtensions.PermissionsFor(guild, channel, await _cache.GetOwnUser(), guildMember);
            var webhookPermissions = PermissionExtensions.EveryonePermissions(guild, channel);
            var userPermissions = PermissionExtensions.PermissionsFor(guild, channel, ctx.Author.Id, senderGuildUser);

            if ((userPermissions & PermissionSet.ViewChannel) == 0)
            {
                // If the user can't see this channel, don't calculate permissions for it
                // (to prevent info-leaking, mostly)
                // Instead, show the user that some channels got ignored (so they don't get confused)
                hiddenChannels = true;
                continue;
            }

            // We use a bitfield so we can set individual permission bits in the loop
            // TODO: Rewrite with proper bitfield math
            ulong missingPermissionField = 0;

            foreach (var requiredPermission in requiredPermissions)
                if ((botPermissions & requiredPermission) == 0)
                    missingPermissionField |= (ulong)requiredPermission;

            if ((webhookPermissions & PermissionSet.UseExternalEmojis) == 0)
            {
                missingPermissionField |= (ulong)PermissionSet.UseExternalEmojis;
                missingEmojiPermissions = true;
            }

            // If we're not missing any permissions, don't bother adding it to the dict
            // This means we can check if the dict is empty to see if all channels are proxyable
            if (missingPermissionField != 0)
            {
                permissionsMissing.TryAdd(missingPermissionField, new List<Channel>());
                permissionsMissing[missingPermissionField].Add(channel);
            }
        }

        // Generate the output embed
        var eb = new EmbedBuilder()
            .Title($"Permission check for **{guild.Name}**");

        if (permissionsMissing.Count == 0)
            eb.Description("No errors found, all channels proxyable :)").Color(DiscordUtils.Green);
        else
            foreach (var (missingPermissionField, channels) in permissionsMissing)
            {
                // Each missing permission field can have multiple missing channels
                // so we extract them all and generate a comma-separated list
                var missingPermissionNames = ((PermissionSet)missingPermissionField).ToPermissionString();

                var channelsList = string.Join("\n", channels
                    .OrderBy(c => c.Position)
                    .Select(c => $"#{c.Name}"));
                eb.Field(new Embed.Field($"Missing *{missingPermissionNames}*", channelsList.Truncate(1000)));
                eb.Color(DiscordUtils.Red);
            }

        var footer = "";
        if (hiddenChannels)
            footer += "Some channels were ignored as you do not have view access to them.";
        if (missingEmojiPermissions)
        {
            if (hiddenChannels) footer += " | ";
            footer +=
                "Use External Emojis permissions must be granted to the @everyone role / Default Permissions.";
        }

        if (footer.Length > 0)
            eb.Footer(new Embed.EmbedFooter(footer));

        // Send! :)
        await ctx.Reply(embed: eb.Build());
    }

    public async Task PermCheckChannel(Context ctx)
    {
        if (!ctx.HasNext())
            throw new PKSyntaxError("You need to specify a channel.");

        var error = "Channel not found or you do not have permissions to access it.";

        // todo: this breaks if channel is not in cache and bot does not have View Channel permissions
        var channel = await ctx.MatchChannel();
        if (channel == null || channel.GuildId == null)
            throw new PKError(error);

        var guild = await _rest.GetGuildOrNull(channel.GuildId.Value);
        if (guild == null)
            throw new PKError(error);

        var guildMember = await _rest.GetGuildMember(channel.GuildId.Value, await _cache.GetOwnUser());

        if (!await ctx.CheckPermissionsInGuildChannel(channel, PermissionSet.ViewChannel))
            throw new PKError(error);

        var botPermissions = PermissionExtensions.PermissionsFor(guild, channel, await _cache.GetOwnUser(), guildMember);
        var webhookPermissions = PermissionExtensions.EveryonePermissions(guild, channel);

        // We use a bitfield so we can set individual permission bits
        ulong missingPermissions = 0;

        foreach (var requiredPermission in requiredPermissions)
            if ((botPermissions & requiredPermission) == 0)
                missingPermissions |= (ulong)requiredPermission;

        if ((webhookPermissions & PermissionSet.UseExternalEmojis) == 0)
            missingPermissions |= (ulong)PermissionSet.UseExternalEmojis;

        // Generate the output embed
        var eb = new EmbedBuilder()
            .Title($"Permission check for **{channel.Name}**");

        if (missingPermissions == 0)
        {
            eb.Description("No issues found, channel is proxyable :)");
        }
        else
        {
            var missing = "";

            foreach (var permission in requiredPermissions)
                if (((ulong)permission & missingPermissions) == (ulong)permission)
                    missing += $"\n- **{permission.ToPermissionString()}**";

            if (((ulong)PermissionSet.UseExternalEmojis & missingPermissions) ==
                (ulong)PermissionSet.UseExternalEmojis)
                missing += $"\n- **{PermissionSet.UseExternalEmojis.ToPermissionString()}**";

            eb.Description($"Missing permissions:\n{missing}");
        }

        await ctx.Reply(embed: eb.Build());
    }

    public async Task MessageProxyCheck(Context ctx)
    {
        if (!ctx.HasNext() && ctx.Message.MessageReference == null)
            throw new PKSyntaxError("You need to specify a message.");

        var failedToGetMessage =
            "Could not find a valid message to check, was not able to fetch the message, or the message was not sent by you.";

        var (messageId, channelId) = ctx.MatchMessage(false);
        if (messageId == null || channelId == null)
            throw new PKError(failedToGetMessage);

        var proxiedMsg = await ctx.Database.Execute(conn => ctx.Repository.GetMessage(conn, messageId.Value));
        if (proxiedMsg != null)
        {
            await ctx.Reply($"{Emojis.Success} This message was proxied successfully.");
            return;
        }

        // get the message info
        var msg = await _rest.GetMessageOrNull(channelId.Value, messageId.Value);
        if (msg == null)
            throw new PKError(failedToGetMessage);

        // if user is fetching a message in a different channel sent by someone else, throw a generic error message
        if (msg == null || msg.Author.Id != ctx.Author.Id && msg.ChannelId != ctx.Channel.Id)
            throw new PKError(failedToGetMessage);

        if ((_botConfig.Prefixes ?? BotConfig.DefaultPrefixes).Any(p => msg.Content.StartsWith(p)))
        {
            await ctx.Reply("This message starts with the bot's prefix, and was parsed as a command.");
            return;
        }
        if (msg.Author.Bot)
            throw new PKError("You cannot check messages sent by a bot.");
        if (msg.WebhookId != null)
            throw new PKError("You cannot check messages sent by a webhook.");
        if (msg.Author.Id != ctx.Author.Id && !ctx.CheckBotAdmin())
            throw new PKError("You can only check your own messages.");

        // get the channel info
        var channel = await _rest.GetChannelOrNull(channelId.Value);
        if (channel == null)
            throw new PKError("Unable to get the channel associated with this message.");

        // using channel.GuildId here since _rest.GetMessage() doesn't return the GuildId
        var context = await ctx.Repository.GetMessageContext(msg.Author.Id, channel.GuildId.Value, msg.ChannelId);
        var members = (await ctx.Repository.GetProxyMembers(msg.Author.Id, channel.GuildId.Value)).ToList();

        // for now this is just server
        var autoproxySettings = await ctx.Repository.GetAutoproxySettings(ctx.System.Id, channel.GuildId.Value, null);

        // todo: match unlatch

        // Run everything through the checks, catch the ProxyCheckFailedException, and reply with the error message.
        try
        {
            _proxy.ShouldProxy(channel, msg, context);
            _matcher.TryMatch(context, autoproxySettings, members, out var match, msg.Content, msg.Attachments.Length > 0, true);

            await ctx.Reply("I'm not sure why this message was not proxied, sorry.");
        }
        catch (ProxyService.ProxyChecksFailedException e)
        {
            await ctx.Reply($"{e.Message}");
        }
    }
}