#nullable enable
using Myriad.Builders;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public class MemberAvatar
{
    private readonly HttpClient _client;

    public MemberAvatar(HttpClient client)
    {
        _client = client;
    }

    private async Task AvatarClear(MemberAvatarLocation location, Context ctx, PKMember target, MemberGuildSettings? mgs)
    {
        await UpdateAvatar(location, ctx, target, null);
        if (location == MemberAvatarLocation.Server)
        {
            if (target.AvatarUrl != null)
                await ctx.Reply(
                    $"{Emojis.Success} Member server avatar cleared. This member will now use the global avatar in this server (**{ctx.Guild.Name}**).");
            else
                await ctx.Reply($"{Emojis.Success} Member server avatar cleared. This member now has no avatar.");
        }
        else if (location == MemberAvatarLocation.MemberWebhook)
        {
            if (mgs?.AvatarUrl != null)
                await ctx.Reply(
                    $"{Emojis.Success} Member proxy avatar cleared. Note that this member has a server-specific avatar set here, type `pk;member {target.Reference(ctx)} serveravatar clear` if you wish to clear that too.");
            else
                await ctx.Reply($"{Emojis.Success} Member proxy avatar cleared. This member will now use the main avatar for proxied messages.");
        }
        else
        {
            if (mgs?.AvatarUrl != null)
                await ctx.Reply(
                    $"{Emojis.Success} Member avatar cleared. Note that this member has a server-specific avatar set here, type `pk;member {target.Reference(ctx)} serveravatar clear` if you wish to clear that too.");
            else
                await ctx.Reply($"{Emojis.Success} Member avatar cleared.");
        }
    }

    private async Task AvatarShow(MemberAvatarLocation location, Context ctx, PKMember target,
                                  MemberGuildSettings? guildData)
    {
        // todo: this privacy code is really confusing
        // for now, we skip privacy flag/config parsing for this, but it would be good to fix that at some point

        var currentValue = location switch
        {
            MemberAvatarLocation.Server => guildData?.AvatarUrl,
            MemberAvatarLocation.MemberWebhook => target.WebhookAvatarUrl,
            MemberAvatarLocation.Member => target.AvatarUrl,
            _ => throw new ArgumentOutOfRangeException(nameof(location))
        };

        var canAccess = location == MemberAvatarLocation.Server ||
                        target.AvatarPrivacy.CanAccess(ctx.DirectLookupContextFor(target.System));

        if (string.IsNullOrEmpty(currentValue) || !canAccess)
        {
            if (location == MemberAvatarLocation.Member)
            {
                if (target.System == ctx.System?.Id)
                    throw new PKSyntaxError(
                        "This member does not have an avatar set. Set one by attaching an image to this command, or by passing an image URL or @mention.");
                throw new PKError("This member does not have an avatar set.");
            }

            if (location == MemberAvatarLocation.MemberWebhook)
                throw new PKError(
                    $"This member does not have a proxy avatar set. Type `pk;member {target.Reference(ctx)} avatar` to see their global avatar.");

            if (location == MemberAvatarLocation.Server)
                throw new PKError(
                    $"This member does not have a server avatar set. Type `pk;member {target.Reference(ctx)} avatar` to see their global avatar.");
        }

        var field = location.Name();
        if (location == MemberAvatarLocation.Server)
            field += $" (for {ctx.Guild.Name})";

        var eb = new EmbedBuilder()
            .Title($"{target.NameFor(ctx)}'s {field}")
            .Image(new Embed.EmbedImage(currentValue?.TryGetCleanCdnUrl()));
        if (target.System == ctx.System?.Id)
            eb.Description($"To clear, use `pk;member {target.Reference(ctx)} {location.Command()} clear`.");
        await ctx.Reply(embed: eb.Build());
    }

    public async Task ServerAvatar(Context ctx, PKMember target)
    {
        ctx.CheckGuildContext();
        var guildData = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);
        await AvatarCommandTree(MemberAvatarLocation.Server, ctx, target, guildData);
    }

    public async Task Avatar(Context ctx, PKMember target)
    {
        var guildData = ctx.Guild != null
            ? await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id)
            : null;

        await AvatarCommandTree(MemberAvatarLocation.Member, ctx, target, guildData);
    }

    public async Task WebhookAvatar(Context ctx, PKMember target)
    {
        var guildData = ctx.Guild != null
            ? await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id)
            : null;

        await AvatarCommandTree(MemberAvatarLocation.MemberWebhook, ctx, target, guildData);
    }

    private async Task AvatarCommandTree(MemberAvatarLocation location, Context ctx, PKMember target,
                                         MemberGuildSettings? guildData)
    {
        // First, see if we need to *clear*
        if (ctx.MatchClear() && await ctx.ConfirmClear("this member's " + location.Name()))
        {
            ctx.CheckSystem().CheckOwnMember(target);
            await AvatarClear(location, ctx, target, guildData);
            return;
        }

        // Then, parse an image from the command (from various sources...)
        var avatarArg = await ctx.MatchImage();
        if (avatarArg == null)
        {
            // If we didn't get any, just show the current avatar
            await AvatarShow(location, ctx, target, guildData);
            return;
        }

        ctx.CheckSystem().CheckOwnMember(target);
        await AvatarUtils.VerifyAvatarOrThrow(_client, avatarArg.Value.Url);
        await UpdateAvatar(location, ctx, target, avatarArg.Value.CleanUrl ?? avatarArg.Value.Url);
        await PrintResponse(location, ctx, target, avatarArg.Value, guildData);
    }

    private Task PrintResponse(MemberAvatarLocation location, Context ctx, PKMember target, ParsedImage avatar,
                               MemberGuildSettings? targetGuildData)
    {
        var serverFrag = location switch
        {
            MemberAvatarLocation.Server =>
                $" This avatar will now be used when proxying in this server (**{ctx.Guild.Name}**).",
            MemberAvatarLocation.MemberWebhook when targetGuildData?.AvatarUrl != null =>
                $" This avatar will now be used for this member's proxied messages, instead of their main avatar.\n{Emojis.Note} Note that this member *also* has a server-specific avatar set in this server (**{ctx.Guild.Name}**), and thus changing the global avatar will have no effect here.",
            MemberAvatarLocation.MemberWebhook =>
                $" This avatar will now be used for this member's proxied messages, instead of their main avatar.",
            MemberAvatarLocation.Member when (targetGuildData?.AvatarUrl != null && target.WebhookAvatarUrl != null) =>
                $"\n{Emojis.Note} Note that this member *also* has a server-specific avatar set in this server (**{ctx.Guild.Name}**), and thus changing the global avatar will have no effect here." +
                $"\n{Emojis.Note} Note that this member *also* has a proxy avatar set, and thus the global avatar will also have no effect on proxied messages in servers without server-specific avatars.",
            MemberAvatarLocation.Member when targetGuildData?.AvatarUrl != null =>
                $"\n{Emojis.Note} Note that this member *also* has a server-specific avatar set in this server (**{ctx.Guild.Name}**), and thus changing the global avatar will have no effect here.",
            MemberAvatarLocation.Member when target.WebhookAvatarUrl != null =>
                $"\n{Emojis.Note} Note that this member *also* has a proxy avatar set, and thus changing the global avatar will have no effect on proxied messages.",
            _ => ""
        };

        var msg = avatar.Source switch
        {
            AvatarSource.User =>
                $"{Emojis.Success} Member {location.Name()} changed to {avatar.SourceUser?.Username}'s avatar!{serverFrag}\n{Emojis.Warn} If {avatar.SourceUser?.Username} changes their avatar, the member's avatar will need to be re-set.",
            AvatarSource.Url =>
                $"{Emojis.Success} Member {location.Name()} changed to the image at the given URL.{serverFrag}",
            AvatarSource.Attachment =>
                $"{Emojis.Success} Member {location.Name()} changed to attached image.{serverFrag}\n{Emojis.Warn} If you delete the message containing the attachment, the avatar will stop working.",
            _ => throw new ArgumentOutOfRangeException()
        };

        // The attachment's already right there, no need to preview it.
        var hasEmbed = avatar.Source != AvatarSource.Attachment;
        return hasEmbed
            ? ctx.Reply(msg, new EmbedBuilder().Image(new Embed.EmbedImage(avatar.Url)).Build())
            : ctx.Reply(msg);
    }

    private Task UpdateAvatar(MemberAvatarLocation location, Context ctx, PKMember target, string? url)
    {
        switch (location)
        {
            case MemberAvatarLocation.Server:
                return ctx.Repository.UpdateMemberGuild(target.Id, ctx.Guild.Id, new MemberGuildPatch { AvatarUrl = url });
            case MemberAvatarLocation.Member:
                return ctx.Repository.UpdateMember(target.Id, new MemberPatch { AvatarUrl = url });
            case MemberAvatarLocation.MemberWebhook:
                return ctx.Repository.UpdateMember(target.Id, new MemberPatch { WebhookAvatarUrl = url });
            default:
                throw new ArgumentOutOfRangeException($"Unknown avatar location {location}");
        }
    }
}
internal enum MemberAvatarLocation
{
    Member,
    MemberWebhook,
    Server,
}

internal static class MemberAvatarLocationExt
{
    public static string Name(this MemberAvatarLocation location)
    {
        return location switch
        {
            MemberAvatarLocation.Server => "server avatar",
            MemberAvatarLocation.MemberWebhook => "proxy avatar",
            MemberAvatarLocation.Member => "avatar",
            _ => throw new ArgumentOutOfRangeException(nameof(location))
        };
    }

    public static string Command(this MemberAvatarLocation location)
    {
        return location switch
        {
            MemberAvatarLocation.Server => "serveravatar",
            MemberAvatarLocation.MemberWebhook => "proxyavatar",
            MemberAvatarLocation.Member => "avatar",
            _ => throw new ArgumentOutOfRangeException(nameof(location))
        };
    }
}