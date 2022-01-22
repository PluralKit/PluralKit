using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Gateway;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using PluralKit.Core;

using NodaTime;

using Serilog;

namespace PluralKit.Bot;

public class ReactionAdded: IEventHandler<MessageReactionAddEvent>
{
    private readonly Bot _bot;
    private readonly IDiscordCache _cache;
    private readonly Cluster _cluster;
    private readonly CommandMessageService _commandMessageService;
    private readonly IDatabase _db;
    private readonly EmbedService _embeds;
    private readonly ILogger _logger;
    private readonly ModelRepository _repo;
    private readonly DiscordApiClient _rest;
    private readonly PrivateChannelService _dmCache;

    public ReactionAdded(ILogger logger, IDatabase db, ModelRepository repo,
                         CommandMessageService commandMessageService, IDiscordCache cache, Bot bot, Cluster cluster,
                         DiscordApiClient rest, EmbedService embeds, PrivateChannelService dmCache)
    {
        _db = db;
        _repo = repo;
        _commandMessageService = commandMessageService;
        _cache = cache;
        _bot = bot;
        _cluster = cluster;
        _rest = rest;
        _embeds = embeds;
        _logger = logger.ForContext<ReactionAdded>();
        _dmCache = dmCache;
    }

    public async Task Handle(int shardId, MessageReactionAddEvent evt)
    {
        await TryHandleProxyMessageReactions(evt);
    }

    private async ValueTask TryHandleProxyMessageReactions(MessageReactionAddEvent evt)
    {
        // ignore any reactions added by *us*
        if (evt.UserId == await _cache.GetOwnUser())
            return;

        // Ignore reactions from bots (we can't DM them anyway)
        // note: this used to get from cache since this event does not contain Member in DMs
        // but we aren't able to get DMs from bots anyway, so it's not really needed
        if (evt.GuildId != null && evt.Member.User.Bot) return;

        var channel = await _cache.GetChannel(evt.ChannelId);

        // check if it's a command message first
        // since this can happen in DMs as well
        if (evt.Emoji.Name == "\u274c")
        {
            // in DMs, allow deleting any PK message
            if (channel.GuildId == null)
            {
                await HandleCommandDeleteReaction(evt, null);
                return;
            }

            var commandMsg = await _commandMessageService.GetCommandMessage(evt.MessageId);
            if (commandMsg != null)
            {
                await HandleCommandDeleteReaction(evt, commandMsg);
                return;
            }
        }

        // Proxied messages only exist in guild text channels, so skip checking if we're elsewhere
        if (!DiscordUtils.IsValidGuildChannel(channel)) return;

        switch (evt.Emoji.Name)
        {
            // Message deletion
            case "\u274C": // Red X
                {
                    var msg = await _db.Execute(c => _repo.GetMessage(c, evt.MessageId));
                    if (msg != null)
                        await HandleProxyDeleteReaction(evt, msg);

                    break;
                }
            case "\u2753": // Red question mark
            case "\u2754": // White question mark
                {
                    var msg = await _db.Execute(c => _repo.GetMessage(c, evt.MessageId));
                    if (msg != null)
                        await HandleQueryReaction(evt, msg);

                    break;
                }

            case "\U0001F514": // Bell
            case "\U0001F6CE": // Bellhop bell
            case "\U0001F3D3": // Ping pong paddle (lol)
            case "\u23F0": // Alarm clock
            case "\u2757": // Exclamation mark
                {
                    var msg = await _db.Execute(c => _repo.GetMessage(c, evt.MessageId));
                    if (msg != null)
                        await HandlePingReaction(evt, msg);
                    break;
                }
        }
    }

    private async ValueTask HandleProxyDeleteReaction(MessageReactionAddEvent evt, FullMessage msg)
    {
        if (!(await _cache.PermissionsIn(evt.ChannelId)).HasFlag(PermissionSet.ManageMessages))
            return;

        var system = await _repo.GetSystemByAccount(evt.UserId);

        // Can only delete your own message
        if (msg.System?.Id != system?.Id && msg.Message.Sender != evt.UserId) return;

        try
        {
            await _rest.DeleteMessage(evt.ChannelId, evt.MessageId);
        }
        catch (NotFoundException)
        {
            // Message was deleted by something/someone else before we got to it
        }

        await _repo.DeleteMessage(evt.MessageId);
    }

    private async ValueTask HandleCommandDeleteReaction(MessageReactionAddEvent evt, CommandMessage? msg)
    {
        // Can only delete your own message
        // (except in DMs, where msg will be null)
        if (msg != null && msg.AuthorId != evt.UserId)
            return;

        // todo: don't try to delete the user's own messages in DMs
        // this is hard since we don't have the message author object, but it happens infrequently enough to not really care about the 403s, I guess?

        try
        {
            await _rest.DeleteMessage(evt.ChannelId, evt.MessageId);
        }
        catch (NotFoundException)
        {
            // Message was deleted by something/someone else before we got to it
        }

        // No need to delete database row here, it'll get deleted by the once-per-minute scheduled task.
    }

    private async ValueTask HandleQueryReaction(MessageReactionAddEvent evt, FullMessage msg)
    {
        var guild = await _cache.GetGuild(evt.GuildId!.Value);

        // Try to DM the user info about the message
        try
        {
            var dm = await _dmCache.GetOrCreateDmChannel(evt.UserId);
            if (msg.Member != null)
                await _rest.CreateMessage(dm, new MessageRequest
                {
                    Embed = await _embeds.CreateMemberEmbed(
                        msg.System,
                        msg.Member,
                        guild,
                        LookupContext.ByNonOwner,
                        DateTimeZone.Utc
                    )
                });

            await _rest.CreateMessage(
                dm,
                new MessageRequest { Embed = await _embeds.CreateMessageInfoEmbed(msg, true) }
            );
        }
        catch (ForbiddenException) { } // No permissions to DM, can't check for this :(

        await TryRemoveOriginalReaction(evt);
    }

    private async ValueTask HandlePingReaction(MessageReactionAddEvent evt, FullMessage msg)
    {
        if (!(await _cache.PermissionsIn(evt.ChannelId)).HasFlag(PermissionSet.ManageMessages))
            return;

        // Check if the "pinger" has permission to send messages in this channel
        // (if not, PK shouldn't send messages on their behalf)
        var member = await _rest.GetGuildMember(evt.GuildId!.Value, evt.UserId);
        var requiredPerms = PermissionSet.ViewChannel | PermissionSet.SendMessages;
        if (member == null || !(await _cache.PermissionsFor(evt.ChannelId, member)).HasFlag(requiredPerms)) return;

        if (msg.Member == null) return;

        var config = await _repo.GetSystemConfig(msg.System.Id);

        if (config.PingsEnabled)
            // If the system has pings enabled, go ahead
            await _rest.CreateMessage(evt.ChannelId, new MessageRequest
            {
                Content = $"Psst, **{msg.Member.DisplayName()}** (<@{msg.Message.Sender}>), you have been pinged by <@{evt.UserId}>.",
                Components = new[]
                {
                    new MessageComponent
                    {
                        Type = ComponentType.ActionRow,
                        Components = new[]
                        {
                            new MessageComponent
                            {
                                Style = ButtonStyle.Link,
                                Type = ComponentType.Button,
                                Label = "Jump",
                                Url = evt.JumpLink()
                            }
                        }
                    }
                },
                AllowedMentions = new AllowedMentions { Users = new[] { msg.Message.Sender } }
            });
        else
            // If not, tell them in DMs (if we can)
            try
            {
                var dm = await _dmCache.GetOrCreateDmChannel(evt.UserId);
                await _rest.CreateMessage(dm,
                    new MessageRequest
                    {
                        Content =
                            $"{Emojis.Error} {msg.Member.DisplayName()}'s system has disabled reaction pings. If you want to mention them anyway, you can copy/paste the following message:"
                    });
                await _rest.CreateMessage(
                    dm,
                    new MessageRequest { Content = $"<@{msg.Message.Sender}>".AsCode() }
                );
            }
            catch (ForbiddenException) { }

        await TryRemoveOriginalReaction(evt);
    }

    private async Task TryRemoveOriginalReaction(MessageReactionAddEvent evt)
    {
        if ((await _cache.PermissionsIn(evt.ChannelId)).HasFlag(PermissionSet.ManageMessages))
            await _rest.DeleteUserReaction(evt.ChannelId, evt.MessageId, evt.Emoji, evt.UserId);
    }
}