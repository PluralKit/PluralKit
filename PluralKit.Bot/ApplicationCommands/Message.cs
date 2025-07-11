using Autofac;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Rest.Types;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot;

public class ApplicationCommandProxiedMessage
{
    private readonly DiscordApiClient _rest;
    private readonly IDiscordCache _cache;
    private readonly EmbedService _embeds;
    private readonly ModelRepository _repo;

    public ApplicationCommandProxiedMessage(DiscordApiClient rest, IDiscordCache cache, EmbedService embeds,
                                            ModelRepository repo)
    {
        _rest = rest;
        _cache = cache;
        _embeds = embeds;
        _repo = repo;
    }

    public async Task QueryMessage(InteractionContext ctx)
    {
        var messageId = ctx.Event.Data!.TargetId!.Value;
        var msg = await ctx.Repository.GetFullMessage(messageId);
        if (msg == null)
        {
            await QueryCommandMessage(ctx);
            return;
        }

        var showContent = true;
        var channel = await _rest.GetChannelOrNull(msg.Message.Channel);
        if (channel == null)
            showContent = false;

        var embeds = new List<Embed>();

        var guild = await _cache.GetGuild(ctx.GuildId);
        if (msg.Member != null)
            embeds.Add(await _embeds.CreateMemberEmbed(
                msg.System,
                msg.Member,
                guild,
                ctx.Config,
                LookupContext.ByNonOwner,
                DateTimeZone.Utc
            ));

        embeds.Add(await _embeds.CreateMessageInfoEmbed(msg, showContent, ctx.Config));

        await ctx.Reply(embeds: embeds.ToArray());
    }

    private async Task QueryCommandMessage(InteractionContext ctx)
    {
        var messageId = ctx.Event.Data!.TargetId!.Value;
        var msg = await ctx.Repository.GetCommandMessage(messageId);
        if (msg == null)
            throw Errors.MessageNotFound(messageId);

        var embeds = new List<Embed>();

        embeds.Add(await _embeds.CreateCommandMessageInfoEmbed(msg, true));

        await ctx.Reply(embeds: embeds.ToArray());
    }

    public async Task DeleteMessage(InteractionContext ctx)
    {
        var messageId = ctx.Event.Data!.TargetId!.Value;

        // check for command messages
        var cmessage = await ctx.Services.Resolve<CommandMessageService>().GetCommandMessage(messageId);
        if (cmessage != null)
        {
            if (cmessage.AuthorId != ctx.User.Id)
                throw new PKError("You can only delete command messages queried by this account.");

            var isDM = (await _repo.GetDmChannel(ctx.User!.Id)) == cmessage.ChannelId;
            await DeleteMessageInner(ctx, cmessage.GuildId, cmessage.ChannelId, messageId, isDM);
            return;
        }

        // and do the same for proxied messages
        var message = await ctx.Repository.GetFullMessage(messageId);
        if (message != null)
        {
            // if user has has a system and their system sent the message, or if user sent the message, do not error
            if (!((ctx.System != null && message.System?.Id == ctx.System.Id) || message.Message.Sender == ctx.User.Id))
                throw new PKError("You can only delete your own messages.");

            await DeleteMessageInner(ctx, message.Message.Guild ?? 0, message.Message.Channel, message.Message.Mid, false);
            return;
        }

        // otherwise, we don't know about this message at all!
        throw Errors.MessageNotFound(messageId);
    }

    internal async Task DeleteMessageInner(InteractionContext ctx, ulong guildId, ulong channelId, ulong messageId, bool isDM = false)
    {
        if (!((await _cache.BotPermissionsIn(guildId, channelId)).HasFlag(PermissionSet.ManageMessages) || isDM))
            throw new PKError("PluralKit does not have the *Manage Messages* permission in this channel, and thus cannot delete the message."
                + " Please contact a server administrator to remedy this.");

        await ctx.Rest.DeleteMessage(channelId, messageId);
        await ctx.Reply($"{Emojis.Success} Message deleted.");
    }

    public async Task PingMessageAuthor(InteractionContext ctx)
    {
        // if the command message was sent by a user account with bot usage disallowed, ignore it
        var abuse_log = await _repo.GetAbuseLogByAccount(ctx.User.Id);
        if (abuse_log != null && abuse_log.DenyBotUsage)
        {
            await ctx.Defer();
            return;
        }

        var messageId = ctx.Event.Data!.TargetId!.Value;
        var msg = await ctx.Repository.GetFullMessage(messageId);
        if (msg == null)
            throw Errors.MessageNotFound(messageId);

        // Check if the "pinger" has permission to send messages in this channel
        // (if not, PK shouldn't send messages on their behalf)
        var member = await _rest.GetGuildMember(ctx.GuildId, ctx.User.Id);
        var requiredPerms = PermissionSet.ViewChannel | PermissionSet.SendMessages;
        if (member == null || !(await _cache.PermissionsForMemberInChannel(ctx.GuildId, ctx.ChannelId, member)).HasFlag(requiredPerms))
        {
            throw new PKError("You do not have permission to send messages in this channel.");
        };

        var config = await _repo.GetSystemConfig(msg.System.Id);

        if (config.PingsEnabled)
        {
            // If the system has pings enabled, go ahead
            await ctx.Respond(InteractionResponse.ResponseType.ChannelMessageWithSource,
                new InteractionApplicationCommandCallbackData
                {
                    Content = $"Psst, **{msg.Member.DisplayName()}** (<@{msg.Message.Sender}>), you have been pinged by <@{ctx.User.Id}>.",
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
                                    Url = msg.Message.JumpLink(),
                                }
                            }
                        }
                    },
                    AllowedMentions = new AllowedMentions { Users = new[] { msg.Message.Sender } },
                    Flags = new() { },
                });
        }
        else
        {
            await ctx.Reply($"{Emojis.Error} {msg.Member.DisplayName()}'s system has disabled command pings.");
        }
    }
}