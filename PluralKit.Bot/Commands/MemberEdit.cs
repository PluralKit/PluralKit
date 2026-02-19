using System.Text.RegularExpressions;

using Myriad.Builders;
using Myriad.Types;

using NodaTime;
using NodaTime.Extensions;

using PluralKit.Core;

namespace PluralKit.Bot;

public class MemberEdit
{
    private readonly HttpClient _client;
    private readonly AvatarHostingService _avatarHosting;

    public MemberEdit(HttpClient client, AvatarHostingService avatarHosting)
    {
        _client = client;
        _avatarHosting = avatarHosting;
    }

    public async Task ShowName(Context ctx, PKMember target, ReplyFormat format)
    {
        var lctx = ctx.DirectLookupContextFor(target.System);
        switch (format)
        {
            case ReplyFormat.Raw:
                await ctx.Reply($"```{target.NameFor(lctx)}```");
                break;
            case ReplyFormat.Plaintext:
                var eb = new EmbedBuilder()
                    .Description($"Showing name for member {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
                await ctx.Reply(target.NameFor(lctx), embed: eb.Build());
                break;
            default:
                var replyStrQ = $"Name for member {target.DisplayHid(ctx.Config)} is **{target.NameFor(lctx)}**.";
                if (target.System == ctx.System?.Id)
                    replyStrQ += $"\nTo rename {target.DisplayHid(ctx.Config)} type `{ctx.DefaultPrefix}member {target.NameFor(ctx)} rename <new name>`."
                    + $" Using {target.NameFor(lctx).Length}/{Limits.MaxMemberNameLength} characters.";
                await ctx.Reply(replyStrQ);
                break;
        }
    }

    public async Task ChangeName(Context ctx, PKMember target, string newName, bool confirmYes)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        // Hard name length cap
        if (newName.Length > Limits.MaxMemberNameLength)
            throw Errors.StringTooLongError("Member name", newName.Length, Limits.MaxMemberNameLength);

        // Warn if there's already a member by this name
        var existingMember = await ctx.Repository.GetMemberByName(ctx.System.Id, newName);
        if (existingMember != null && existingMember.Id != target.Id)
        {
            var msg =
                $"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.NameFor(ctx)}\" (`{existingMember.DisplayHid(ctx.Config)}`). Do you want to rename this member to that name too?";
            if (!await ctx.PromptYesNo(msg, "Rename", flagValue: confirmYes)) throw new PKError("Member renaming cancelled.");
        }

        // Rename the member
        var patch = new MemberPatch { Name = Partial<string>.Present(newName) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        var replyStr = $"{Emojis.Success} Member renamed (using {newName.Length}/{Limits.MaxMemberNameLength} characters).";
        if (newName.Contains(" "))
            replyStr += $"\n{Emojis.Note} Note that this member's name now contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it, or just use the member's short ID (which is `{target.DisplayHid(ctx.Config)}`).";
        if (target.DisplayName != null)
            replyStr += $"\n{Emojis.Note} Note that this member has a display name set ({target.DisplayName}), and will be proxied using that name instead.";

        if (ctx.Guild != null)
        {
            var memberGuildConfig = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);
            if (memberGuildConfig.DisplayName != null)
                replyStr += $"\n{Emojis.Note} Note that this member has a server name set ({memberGuildConfig.DisplayName}) in this server ({ctx.Guild.Name}), and will be proxied using that name here.";
        }
        await ctx.Reply(replyStr);
    }

    public async Task ShowDescription(Context ctx, PKMember target, ReplyFormat format)
    {
        ctx.CheckSystemPrivacy(target.System, target.DescriptionPrivacy);

        var noDescriptionSetMessage = "This member does not have a description set.";
        if (ctx.System?.Id == target.System)
            noDescriptionSetMessage +=
                $" To set one, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} description <description>`.";

        // if there's nothing next or what's next is "raw"/"plaintext" we're doing a query, so check for null
        if (format != ReplyFormat.Standard)
            if (target.Description == null)
            {
                await ctx.Reply(noDescriptionSetMessage);
                return;
            }

        if (format == ReplyFormat.Raw)
        {
            await ctx.Reply($"```\n{target.Description}\n```");
            return;
        }
        if (format == ReplyFormat.Plaintext)
        {
            var eb = new EmbedBuilder()
                .Description($"Showing description for member {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
            await ctx.Reply(target.Description, embed: eb.Build());
            return;
        }

        await ctx.Reply(embed: new EmbedBuilder()
            .Title("Member description")
            .Description(target.Description)
            .Field(new Embed.Field("\u200B",
                $"To print the description with formatting, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} description -raw`."
                + (ctx.System?.Id == target.System
                    ? $" To clear it, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} description -clear`."
                    + $" Using {target.Description.Length}/{Limits.MaxDescriptionLength} characters."
                    : "")))
            .Build());
    }

    public async Task ClearDescription(Context ctx, PKMember target, bool confirmYes)
    {
        ctx.CheckSystemPrivacy(target.System, target.DescriptionPrivacy);
        ctx.CheckOwnMember(target);

        if (await ctx.ConfirmClear("this member's description", confirmYes))
        {
            var patch = new MemberPatch { Description = Partial<string>.Null() };
            await ctx.Repository.UpdateMember(target.Id, patch);
            await ctx.Reply($"{Emojis.Success} Member description cleared.");
        }
    }

    public async Task ChangeDescription(Context ctx, PKMember target, string _description)
    {
        ctx.CheckSystemPrivacy(target.System, target.DescriptionPrivacy);
        ctx.CheckOwnMember(target);

        var description = _description.NormalizeLineEndSpacing();
        if (description.IsLongerThan(Limits.MaxDescriptionLength))
            throw Errors.StringTooLongError("Description", description.Length, Limits.MaxDescriptionLength);

        var patch = new MemberPatch { Description = Partial<string>.Present(description) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        await ctx.Reply($"{Emojis.Success} Member description changed (using {description.Length}/{Limits.MaxDescriptionLength} characters).");
    }

    public async Task ShowPronouns(Context ctx, PKMember target, ReplyFormat format)
    {
        ctx.CheckSystemPrivacy(target.System, target.PronounPrivacy);

        var noPronounsSetMessage = "This member does not have pronouns set.";
        if (ctx.System?.Id == target.System)
            noPronounsSetMessage += $" To set some, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} pronouns <pronouns>`.";

        // check for null since we are doing a query
        if (target.Pronouns == null)
        {
            await ctx.Reply(noPronounsSetMessage);
            return;
        }

        if (format == ReplyFormat.Raw)
        {
            await ctx.Reply($"```\n{target.Pronouns}\n```");
            return;
        }
        if (format == ReplyFormat.Plaintext)
        {
            var eb = new EmbedBuilder()
                .Description($"Showing pronouns for member {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
            await ctx.Reply(target.Pronouns, embed: eb.Build());
            return;
        }

        await ctx.Reply(
            $"**{target.NameFor(ctx)}**'s pronouns are **{target.Pronouns}**.\nTo print the pronouns with formatting, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} pronouns -raw`."
            + (ctx.System?.Id == target.System
                ? $" To clear them, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} pronouns -clear`."
                + $" Using {target.Pronouns.Length}/{Limits.MaxPronounsLength} characters."
                : ""));
    }

    public async Task ClearPronouns(Context ctx, PKMember target, bool confirmYes)
    {
        ctx.CheckOwnMember(target);

        if (await ctx.ConfirmClear("this member's pronouns", confirmYes))
        {
            var patch = new MemberPatch { Pronouns = Partial<string>.Null() };
            await ctx.Repository.UpdateMember(target.Id, patch);
            await ctx.Reply($"{Emojis.Success} Member pronouns cleared.");
        }
    }

    public async Task ChangePronouns(Context ctx, PKMember target, string pronouns)
    {
        ctx.CheckOwnMember(target);

        pronouns = pronouns.NormalizeLineEndSpacing();
        if (pronouns.IsLongerThan(Limits.MaxPronounsLength))
            throw Errors.StringTooLongError("Pronouns", pronouns.Length, Limits.MaxPronounsLength);

        var patch = new MemberPatch { Pronouns = Partial<string>.Present(pronouns) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        await ctx.Reply($"{Emojis.Success} Member pronouns changed (using {pronouns.Length}/{Limits.MaxPronounsLength} characters).");
    }

    public async Task ShowBannerImage(Context ctx, PKMember target, ReplyFormat format)
    {
        ctx.CheckSystemPrivacy(target.System, target.BannerPrivacy);

        var noBannerSetMessage = "This member does not have a banner image set.";
        if (ctx.System?.Id == target.System)
            noBannerSetMessage += $" To set one, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} banner <url>` or attach an image.";

        if (format != ReplyFormat.Standard)
            if (string.IsNullOrWhiteSpace(target.BannerImage))
            {
                await ctx.Reply(noBannerSetMessage);
                return;
            }

        if (format == ReplyFormat.Raw)
        {
            await ctx.Reply($"```\n{target.BannerImage.TryGetCleanCdnUrl()}\n```");
            return;
        }
        if (format == ReplyFormat.Plaintext)
        {
            var eb = new EmbedBuilder()
                .Description($"Showing banner for member {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
            await ctx.Reply(text: $"<{target.BannerImage.TryGetCleanCdnUrl()}>", embed: eb.Build());
            return;
        }

        var embed = new EmbedBuilder()
            .Title($"{target.NameFor(ctx)}'s banner image")
            .Image(new Embed.EmbedImage(target.BannerImage.TryGetCleanCdnUrl()));
        if (target.System == ctx.System?.Id)
            embed.Description($"To clear, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} banner -clear`.");
        await ctx.Reply(embed: embed.Build());
    }

    public async Task ClearBannerImage(Context ctx, PKMember target, bool confirmYes)
    {
        ctx.CheckOwnMember(target);

        if (await ctx.ConfirmClear("this member's banner image", confirmYes))
        {
            await ctx.Repository.UpdateMember(target.Id, new MemberPatch { BannerImage = null });
            await ctx.Reply($"{Emojis.Success} Member banner image cleared.");
        }
    }

    public async Task ChangeBannerImage(Context ctx, PKMember target, ParsedImage img)
    {
        ctx.CheckOwnMember(target);

        img = await _avatarHosting.TryRehostImage(img, AvatarHostingService.RehostedImageType.Banner, ctx.Author.Id, ctx.System);
        await _avatarHosting.VerifyAvatarOrThrow(img.Url, true);

        await ctx.Repository.UpdateMember(target.Id, new MemberPatch { BannerImage = img.CleanUrl ?? img.Url });

        var msg = img.Source switch
        {
            AvatarSource.Url => $"{Emojis.Success} Member banner image changed to the image at the given URL.",
            AvatarSource.HostedCdn => $"{Emojis.Success} Member banner image changed to attached image.",
            AvatarSource.Attachment =>
                $"{Emojis.Success} Member banner image changed to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the banner image will stop working.",
            AvatarSource.User => throw new PKError("Cannot set a banner image to an user's avatar."),
            _ => throw new ArgumentOutOfRangeException()
        };

        // The attachment's already right there, no need to preview it.
        var hasEmbed = img.Source != AvatarSource.Attachment && img.Source != AvatarSource.HostedCdn;
        await (hasEmbed
            ? ctx.Reply(msg, new EmbedBuilder().Image(new Embed.EmbedImage(img.Url)).Build())
            : ctx.Reply(msg));
    }

    public async Task ShowColor(Context ctx, PKMember target, ReplyFormat format)
    {
        if (target.Color == null)
        {
            await ctx.Reply(
                "This member does not have a color set." + (ctx.System?.Id == target.System ? $" To set one, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} color <color>`." : ""));
            return;
        }

        if (format == ReplyFormat.Raw)
        {
            await ctx.Reply("```\n#" + target.Color + "\n```");
            return;
        }

        if (format == ReplyFormat.Plaintext)
        {
            await ctx.Reply(target.Color);
            return;
        }

        await ctx.Reply(embed: new EmbedBuilder()
            .Title("Member color")
            .Color(target.Color.ToDiscordColor())
            .Thumbnail(new Embed.EmbedThumbnail($"attachment://color.gif"))
            .Description($"This member's color is **#{target.Color}**."
                + (ctx.System?.Id == target.System ? $" To clear it, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} color -clear`." : ""))
            .Build(),
            files: [MiscUtils.GenerateColorPreview(target.Color)]);
    }

    public async Task ClearColor(Context ctx, PKMember target, bool confirmYes)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        if (await ctx.ConfirmClear("this member's color", confirmYes))
        {
            await ctx.Repository.UpdateMember(target.Id, new() { Color = Partial<string>.Null() });
            await ctx.Reply($"{Emojis.Success} Member color cleared.");
        }
    }

    public async Task ChangeColor(Context ctx, PKMember target, string color)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        if (color.StartsWith("#"))
            color = color.Substring(1);

        if (!Regex.IsMatch(color, "^[0-9a-fA-F]{6}$"))
            throw Errors.InvalidColorError(color);

        var patch = new MemberPatch { Color = Partial<string>.Present(color.ToLowerInvariant()) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        await ctx.Reply(embed: new EmbedBuilder()
            .Title($"{Emojis.Success} Member color changed.")
            .Color(color.ToDiscordColor())
            .Thumbnail(new Embed.EmbedThumbnail($"attachment://color.gif"))
            .Build(),
            files: [MiscUtils.GenerateColorPreview(color)]);
    }

    public async Task ShowBirthday(Context ctx, PKMember target, ReplyFormat format)
    {
        ctx.CheckSystemPrivacy(target.System, target.BirthdayPrivacy);

        var noBirthdaySetMessage = "This member does not have a birthdate set.";
        if (ctx.System?.Id == target.System)
            noBirthdaySetMessage += $" To set one, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} birthday <birthdate>`.";

        // if what's next is "raw"/"plaintext" we need to check for null
        if (format != ReplyFormat.Standard)
            if (target.Birthday == null)
            {
                await ctx.Reply(noBirthdaySetMessage);
                return;
            }

        if (format == ReplyFormat.Raw)
        {
            await ctx.Reply($"```\n{target.Birthday}\n```");
            return;
        }
        if (format == ReplyFormat.Plaintext)
        {
            var eb = new EmbedBuilder()
                .Description($"Showing birthday for member {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
            await ctx.Reply(target.BirthdayString, embed: eb.Build());
            return;
        }

        await ctx.Reply($"This member's birthdate is **{target.BirthdayString}**."
            + (ctx.System?.Id == target.System
                ? $" To clear it, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} birthday -clear`."
                : ""));
    }

    public async Task ClearBirthday(Context ctx, PKMember target, bool confirmYes)
    {
        ctx.CheckOwnMember(target);

        if (await ctx.ConfirmClear("this member's birthday", confirmYes))
        {
            var patch = new MemberPatch { Birthday = Partial<LocalDate?>.Null() };
            await ctx.Repository.UpdateMember(target.Id, patch);
            await ctx.Reply($"{Emojis.Success} Member birthdate cleared.");
        }
    }

    public async Task ChangeBirthday(Context ctx, PKMember target, string birthdayStr)
    {
        ctx.CheckOwnMember(target);

        LocalDate? birthday;
        if (birthdayStr == "today" || birthdayStr == "now")
            birthday = SystemClock.Instance.InZone(ctx.Zone).GetCurrentDate();
        else
            birthday = DateUtils.ParseDate(birthdayStr, true);

        if (birthday == null)
            throw Errors.BirthdayParseError(birthdayStr);

        var patch = new MemberPatch { Birthday = Partial<LocalDate?>.Present(birthday) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        await ctx.Reply($"{Emojis.Success} Member birthdate changed.");
    }

    private string boldIf(string str, bool condition) => condition ? $"**{str}**" : str;

    private async Task<EmbedBuilder> CreateMemberNameInfoEmbed(Context ctx, PKMember target)
    {
        var lcx = ctx.LookupContextFor(target.System);

        MemberGuildSettings memberGuildConfig = null;
        if (ctx.Guild != null)
            memberGuildConfig = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);

        var eb = new EmbedBuilder()
            .Title("Member names")
            .Footer(new Embed.EmbedFooter(
                $"Member ID: {target.DisplayHid(ctx.Config)} | Active name in bold. Server name overrides display name, which overrides base name."
                + (target.DisplayName != null && ctx.System?.Id == target.System ? $" Using {target.DisplayName.Length}/{Limits.MaxMemberNameLength} characters for the display name." : "")
                + (memberGuildConfig?.DisplayName != null && ctx.System?.Id == target.System ? $" Using {memberGuildConfig?.DisplayName.Length}/{Limits.MaxMemberNameLength} characters for the server name." : "")));

        var showDisplayName = target.NamePrivacy.CanAccess(lcx);

        eb.Field(new Embed.Field("Name", boldIf(
            target.NameFor(ctx),
            (!showDisplayName || target.DisplayName == null) && memberGuildConfig?.DisplayName == null
        )));

        eb.Field(new Embed.Field("Display name", (target.DisplayName != null && showDisplayName)
            ? boldIf(target.DisplayName, memberGuildConfig?.DisplayName == null)
            : "*(no displayname set or name is private)*"
        ));

        if (ctx.Guild != null)
            eb.Field(new Embed.Field($"Server Name (in {ctx.Guild.Name})",
                memberGuildConfig?.DisplayName != null
                    ? $"**{memberGuildConfig.DisplayName}**"
                    : "*(none)*"
            ));

        return eb;
    }

    public async Task ShowDisplayName(Context ctx, PKMember target, ReplyFormat format)
    {
        var isOwner = ctx.System?.Id == target.System;
        var noDisplayNameSetMessage = $"This member does not have a display name set{(isOwner ? "" : " or name is private")}."
            + (isOwner ? $" To set one, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} displayname <display name>`." : "");

        // Whether displayname is shown or not should depend on if member name privacy is set.
        // If name privacy is on then displayname should look like name.
        if (target.DisplayName == null || !target.NamePrivacy.CanAccess(ctx.DirectLookupContextFor(target.System)))
        {
            await ctx.Reply(noDisplayNameSetMessage);
            return;
        }

        if (format == ReplyFormat.Raw)
        {
            await ctx.Reply($"```\n{target.DisplayName}\n```");
            return;
        }
        if (format == ReplyFormat.Plaintext)
        {
            var _eb = new EmbedBuilder()
                .Description($"Showing displayname for member {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
            await ctx.Reply(target.DisplayName, embed: _eb.Build());
            return;
        }

        var eb = await CreateMemberNameInfoEmbed(ctx, target);
        var reference = target.Reference(ctx);
        if (ctx.System?.Id == target.System)
            eb.Description(
                $"To change display name, type `{ctx.DefaultPrefix}member {reference} displayname <display name>`.\n"
                + $"To clear it, type `{ctx.DefaultPrefix}member {reference} displayname -clear`.\n"
                + $"To print the raw display name, type `{ctx.DefaultPrefix}member {reference} displayname -raw`.");
        await ctx.Reply(embed: eb.Build());
    }

    public async Task ClearDisplayName(Context ctx, PKMember target, bool confirmYes)
    {
        ctx.CheckOwnMember(target);

        if (await ctx.ConfirmClear("this member's display name", confirmYes))
        {
            var patch = new MemberPatch { DisplayName = Partial<string>.Null() };
            await ctx.Repository.UpdateMember(target.Id, patch);

            var successStr = $"{Emojis.Success} Member display name cleared. This member will now be proxied using their member name \"{target.Name}\".";

            if (ctx.Guild != null)
            {
                var memberGuildConfig = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);
                if (memberGuildConfig.DisplayName != null)
                    successStr +=
                        $" However, this member has a server name set in this server ({ctx.Guild.Name}), and will be proxied using that name, \"{memberGuildConfig.DisplayName}\", here.";
            }

            await ctx.Reply(successStr);

            if (target.NamePrivacy == PrivacyLevel.Private)
                await ctx.Reply($"{Emojis.Warn} Since this member no longer has a display name set, their name privacy **can no longer take effect**.");
        }
    }

    public async Task ChangeDisplayName(Context ctx, PKMember target, string newDisplayName)
    {
        ctx.CheckOwnMember(target);

        newDisplayName = newDisplayName.NormalizeLineEndSpacing();
        if (newDisplayName.Length > Limits.MaxMemberNameLength)
            throw Errors.StringTooLongError("Member display name", newDisplayName.Length, Limits.MaxMemberNameLength);

        var patch = new MemberPatch { DisplayName = Partial<string>.Present(newDisplayName) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        var successStr = $"{Emojis.Success} Member display name changed (using {newDisplayName.Length}/{Limits.MaxMemberNameLength} characters). This member will now be proxied using the name \"{newDisplayName}\".";

        if (ctx.Guild != null)
        {
            var memberGuildConfig = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);
            if (memberGuildConfig.DisplayName != null)
                successStr +=
                    $" However, this member has a server name set in this server ({ctx.Guild.Name}), and will be proxied using that name, \"{memberGuildConfig.DisplayName}\", here.";
        }

        await ctx.Reply(successStr);
    }

    public async Task ShowServerName(Context ctx, PKMember target, ReplyFormat format)
    {
        ctx.CheckGuildContext();

        var noServerNameSetMessage = "This member does not have a server name set.";
        if (ctx.System?.Id == target.System)
            noServerNameSetMessage +=
                $" To set one, type `{ctx.DefaultPrefix}member {target.Reference(ctx)} servername <server name>`.";

        var memberGuildConfig = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);

        if (format != ReplyFormat.Standard)
            if (memberGuildConfig.DisplayName == null)
            {
                await ctx.Reply(noServerNameSetMessage);
                return;
            }

        if (format == ReplyFormat.Raw)
        {
            await ctx.Reply($"```\n{memberGuildConfig.DisplayName}\n```");
            return;
        }
        if (format == ReplyFormat.Plaintext)
        {
            var _eb = new EmbedBuilder()
                .Description($"Showing servername for member {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
            await ctx.Reply(memberGuildConfig.DisplayName, embed: _eb.Build());
            return;
        }

        var eb = await CreateMemberNameInfoEmbed(ctx, target);
        var reference = target.Reference(ctx);
        if (ctx.System?.Id == target.System)
            eb.Description(
                $"To change server name, type `{ctx.DefaultPrefix}member {reference} servername <server name>`.\nTo clear it, type `{ctx.DefaultPrefix}member {reference} servername -clear`.\nTo print the raw server name, type `{ctx.DefaultPrefix}member {reference} servername -raw`.");
        await ctx.Reply(embed: eb.Build());
    }

    public async Task ClearServerName(Context ctx, PKMember target, bool confirmYes)
    {
        ctx.CheckGuildContext();
        ctx.CheckOwnMember(target);

        if (await ctx.ConfirmClear("this member's server name", confirmYes))
        {
            await ctx.Repository.UpdateMemberGuild(target.Id, ctx.Guild.Id, new MemberGuildPatch { DisplayName = null });

            if (target.DisplayName != null)
                await ctx.Reply(
                    $"{Emojis.Success} Member server name cleared. This member will now be proxied using their global display name \"{target.DisplayName}\" in this server ({ctx.Guild.Name}).");
            else
                await ctx.Reply(
                    $"{Emojis.Success} Member server name cleared. This member will now be proxied using their member name \"{target.NameFor(ctx)}\" in this server ({ctx.Guild.Name}).");
        }
    }

    public async Task ChangeServerName(Context ctx, PKMember target, string newServerName)
    {
        ctx.CheckGuildContext();
        ctx.CheckOwnMember(target);

        newServerName = newServerName.NormalizeLineEndSpacing();
        if (newServerName.Length > Limits.MaxMemberNameLength)
            throw Errors.StringTooLongError("Server name", newServerName.Length, Limits.MaxMemberNameLength);

        await ctx.Repository.UpdateMemberGuild(target.Id, ctx.Guild.Id,
            new MemberGuildPatch { DisplayName = newServerName });

        await ctx.Reply(
            $"{Emojis.Success} Member server name changed (using {newServerName.Length}/{Limits.MaxMemberNameLength} characters). This member will now be proxied using the name \"{newServerName}\" in this server ({ctx.Guild.Name}).");
    }

    public async Task ShowKeepProxy(Context ctx, PKMember target)
    {
        string keepProxyStatusMessage = "";

        if (target.KeepProxy)
            keepProxyStatusMessage += "This member has keepproxy **enabled**. Proxy tags will be **included** in the resulting message when proxying.";
        else
            keepProxyStatusMessage += "This member has keepproxy **disabled**. Proxy tags will **not** be included in the resulting message when proxying.";

        if (ctx.Guild != null)
        {
            var memberGuildConfig = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);

            if (memberGuildConfig?.KeepProxy.HasValue == true)
            {
                if (memberGuildConfig.KeepProxy.Value)
                    keepProxyStatusMessage += $"\n{Emojis.Warn} This member has keepproxy **enabled in this server**, which means proxy tags will **always** be included when proxying in this server, regardless of the global keepproxy. To clear this setting in this server, type `{ctx.DefaultPrefix}m <member> serverkeepproxy clear`.";
                else
                    keepProxyStatusMessage += $"\n{Emojis.Warn} This member has keepproxy **disabled in this server**, which means proxy tags will **never** be included when proxying in this server, regardless of the global keepproxy. To clear this setting in this server, type `{ctx.DefaultPrefix}m <member> serverkeepproxy clear`.";
            }
        }

        await ctx.Reply(keepProxyStatusMessage);
    }

    public async Task ChangeKeepProxy(Context ctx, PKMember target, bool newValue)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        var patch = new MemberPatch { KeepProxy = Partial<bool>.Present(newValue) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        string keepProxyUpdateMessage = "";

        if (newValue)
            keepProxyUpdateMessage += $"{Emojis.Success} this member now has keepproxy **enabled**. Member proxy tags will be **included** in the resulting message when proxying.";
        else
            keepProxyUpdateMessage += $"{Emojis.Success} this member now has keepproxy **disabled**. Member proxy tags will **not** be included in the resulting message when proxying.";

        if (ctx.Guild != null)
        {
            var memberGuildConfig = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);

            if (memberGuildConfig?.KeepProxy.HasValue == true)
            {
                if (memberGuildConfig.KeepProxy.Value)
                    keepProxyUpdateMessage += $"\n{Emojis.Warn} This member has keepproxy **enabled in this server**, which means proxy tags will **always** be included when proxying in this server, regardless of the global keepproxy. To clear this setting in this server, type `{ctx.DefaultPrefix}m <member> serverkeepproxy clear`.";
                else
                    keepProxyUpdateMessage += $"\n{Emojis.Warn} This member has keepproxy **disabled in this server**, which means proxy tags will **never** be included when proxying in this server, regardless of the global keepproxy. To clear this setting in this server, type `{ctx.DefaultPrefix}m <member> serverkeepproxy clear`.";
            }
        }

        await ctx.Reply(keepProxyUpdateMessage);
    }

    public async Task ShowServerKeepProxy(Context ctx, PKMember target)
    {
        ctx.CheckGuildContext();

        var memberGuildConfig = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);

        if (memberGuildConfig.KeepProxy.HasValue)
        {
            if (memberGuildConfig.KeepProxy.Value)
                await ctx.Reply($"This member has keepproxy **enabled** in the current server, which means proxy tags will be **included** in the resulting message when proxying. To clear this setting in this server, type `{ctx.DefaultPrefix}m <member> serverkeepproxy clear`.");
            else
                await ctx.Reply($"This member has keepproxy **disabled** in the current server, which means proxy tags will **not** be included in the resulting message when proxying. To clear this setting in this server, type `{ctx.DefaultPrefix}m <member> serverkeepproxy clear`.");
        }
        else
        {
            var noServerKeepProxySetMessage = "This member does not have a server keepproxy override set.";
            if (target.KeepProxy)
                noServerKeepProxySetMessage += " The global keepproxy is **enabled**, which means proxy tags will be **included** when proxying.";
            else
                noServerKeepProxySetMessage += " The global keepproxy is **disabled**, which means proxy tags will **not** be included when proxying.";

            await ctx.Reply(noServerKeepProxySetMessage);
        }
    }

    public async Task ClearServerKeepProxy(Context ctx, PKMember target, bool confirmYes)
    {
        ctx.CheckGuildContext();
        ctx.CheckSystem().CheckOwnMember(target);

        if (await ctx.ConfirmClear("this member's server keepproxy setting", confirmYes))
        {
            var patch = new MemberGuildPatch { KeepProxy = Partial<bool?>.Present(null) };
            await ctx.Repository.UpdateMemberGuild(target.Id, ctx.Guild.Id, patch);

            var serverKeepProxyClearedMessage = $"{Emojis.Success} Cleared server keepproxy settings for this member.";
            if (target.KeepProxy)
                serverKeepProxyClearedMessage += " Member proxy tags will now be **included** in the resulting message when proxying.";
            else
                serverKeepProxyClearedMessage += " Member proxy tags will now **not** be included in the resulting message when proxying.";

            await ctx.Reply(serverKeepProxyClearedMessage);
        }
    }

    public async Task ChangeServerKeepProxy(Context ctx, PKMember target, bool newValue)
    {
        ctx.CheckGuildContext();
        ctx.CheckSystem().CheckOwnMember(target);

        var patch = new MemberGuildPatch { KeepProxy = Partial<bool?>.Present(newValue) };
        await ctx.Repository.UpdateMemberGuild(target.Id, ctx.Guild.Id, patch);

        if (newValue)
            await ctx.Reply($"{Emojis.Success} Member proxy tags will now be **included** in the resulting message when proxying **in the current server**. To clear this setting in this server, type `{ctx.DefaultPrefix}m <member> serverkeepproxy clear`.");
        else
            await ctx.Reply($"{Emojis.Success} Member proxy tags will now **not** be included in the resulting message when proxying **in the current server**. To clear this setting in this server, type `{ctx.DefaultPrefix}m <member> serverkeepproxy clear`.");
    }

    public async Task ShowTts(Context ctx, PKMember target)
    {
        if (target.Tts)
            await ctx.Reply(
                "This member has text-to-speech **enabled**, which means their messages **will be** sent as text-to-speech messages.");
        else
            await ctx.Reply(
                "This member has text-to-speech **disabled**, which means their messages **will not** be sent as text-to-speech messages.");
    }

    public async Task ChangeTts(Context ctx, PKMember target, bool newValue)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        var patch = new MemberPatch { Tts = Partial<bool>.Present(newValue) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        if (newValue)
            await ctx.Reply(
                $"{Emojis.Success} Member's messages will now be sent as text-to-speech messages.");
        else
            await ctx.Reply(
                $"{Emojis.Success} Member messages will no longer be sent as text-to-speech messages.");
    }

    public async Task ShowAutoproxy(Context ctx, PKMember target)
    {
        if (target.AllowAutoproxy)
            await ctx.Reply(
                "Latch/front autoproxy are **enabled** for this member. This member will be automatically proxied when autoproxy is set to latch or front mode.");
        else
            await ctx.Reply(
                "Latch/front autoproxy are **disabled** for this member. This member will not be automatically proxied when autoproxy is set to latch or front mode.");
    }

    public async Task ChangeAutoproxy(Context ctx, PKMember target, bool newValue)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        var patch = new MemberPatch { AllowAutoproxy = Partial<bool>.Present(newValue) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        if (newValue)
            await ctx.Reply($"{Emojis.Success} Latch / front autoproxy have been **enabled** for this member.");
        else
            await ctx.Reply($"{Emojis.Success} Latch / front autoproxy have been **disabled** for this member.");
    }

    public async Task ShowPrivacy(Context ctx, PKMember target)
    {
        await ctx.Reply(embed: new EmbedBuilder()
            .Title($"Current privacy settings for {target.NameFor(ctx)}")
            .Field(new Embed.Field("Name (replaces name with display name if member has one)",
                target.NamePrivacy.Explanation()))
            .Field(new Embed.Field("Description", target.DescriptionPrivacy.Explanation()))
            .Field(new Embed.Field("Banner", target.BannerPrivacy.Explanation()))
            .Field(new Embed.Field("Avatar", target.AvatarPrivacy.Explanation()))
            .Field(new Embed.Field("Birthday", target.BirthdayPrivacy.Explanation()))
            .Field(new Embed.Field("Pronouns", target.PronounPrivacy.Explanation()))
            .Field(new Embed.Field("Proxy Tags", target.ProxyPrivacy.Explanation()))
            .Field(new Embed.Field("Meta (creation date, message count, last front, last message)",
                target.MetadataPrivacy.Explanation()))
            .Field(new Embed.Field("Visibility", target.MemberVisibility.Explanation()))
            .Description(
                $"To edit privacy settings, use the command:\n`{ctx.DefaultPrefix}member <member> privacy <subject> <level>`\n\n- `subject` is one of `name`, `description`, `banner`, `avatar`, `birthday`, `pronouns`, `proxies`, `metadata`, `visibility`, or `all`\n- `level` is either `public` or `private`.")
            .Build());
    }

    public async Task ChangeAllPrivacy(Context ctx, PKMember target, PrivacyLevel level)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        await ctx.Repository.UpdateMember(target.Id, new MemberPatch().WithAllPrivacy(level));

        if (level == PrivacyLevel.Private)
            await ctx.Reply(
                $"{Emojis.Success} All {target.NameFor(ctx)}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see nothing on the member card.");
        else
            await ctx.Reply(
                $"{Emojis.Success} All {target.NameFor(ctx)}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see everything on the member card.");
    }

    public async Task ChangePrivacy(Context ctx, PKMember target, MemberPrivacySubject subject, PrivacyLevel level)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        await ctx.Repository.UpdateMember(target.Id, new MemberPatch().WithPrivacy(subject, level));

        var subjectName = subject switch
        {
            MemberPrivacySubject.Name => "name privacy",
            MemberPrivacySubject.Description => "description privacy",
            MemberPrivacySubject.Banner => "banner privacy",
            MemberPrivacySubject.Avatar => "avatar privacy",
            MemberPrivacySubject.Pronouns => "pronoun privacy",
            MemberPrivacySubject.Birthday => "birthday privacy",
            MemberPrivacySubject.Proxy => "proxy tag privacy",
            MemberPrivacySubject.Metadata => "metadata privacy",
            MemberPrivacySubject.Visibility => "visibility",
            _ => throw new ArgumentOutOfRangeException($"Unknown privacy subject {subject}")
        };

        var explanation = (subject, level) switch
        {
            (MemberPrivacySubject.Name, PrivacyLevel.Private) =>
                "This member's name is now hidden from other systems, and will be replaced by the member's display name.",
            (MemberPrivacySubject.Description, PrivacyLevel.Private) =>
                "This member's description is now hidden from other systems.",
            (MemberPrivacySubject.Banner, PrivacyLevel.Private) =>
                "This member's banner is now hidden from other systems.",
            (MemberPrivacySubject.Avatar, PrivacyLevel.Private) =>
                "This member's avatar is now hidden from other systems.",
            (MemberPrivacySubject.Birthday, PrivacyLevel.Private) =>
                "This member's birthday is now hidden from other systems.",
            (MemberPrivacySubject.Pronouns, PrivacyLevel.Private) =>
                "This member's pronouns are now hidden from other systems.",
            (MemberPrivacySubject.Proxy, PrivacyLevel.Private) =>
                "This member's proxy tags are now hidden from other systems.",
            (MemberPrivacySubject.Metadata, PrivacyLevel.Private) =>
                "This member's metadata (eg. created timestamp, message count, etc) is now hidden from other systems.",
            (MemberPrivacySubject.Visibility, PrivacyLevel.Private) =>
                "This member is now hidden from member lists.",

            (MemberPrivacySubject.Name, PrivacyLevel.Public) =>
                "This member's name is no longer hidden from other systems.",
            (MemberPrivacySubject.Description, PrivacyLevel.Public) =>
                "This member's description is no longer hidden from other systems.",
            (MemberPrivacySubject.Banner, PrivacyLevel.Public) =>
                "This member's banner is no longer hidden from other systems.",
            (MemberPrivacySubject.Avatar, PrivacyLevel.Public) =>
                "This member's avatar is no longer hidden from other systems.",
            (MemberPrivacySubject.Birthday, PrivacyLevel.Public) =>
                "This member's birthday is no longer hidden from other systems.",
            (MemberPrivacySubject.Pronouns, PrivacyLevel.Public) =>
                "This member's pronouns are no longer hidden from other systems.",
            (MemberPrivacySubject.Proxy, PrivacyLevel.Public) =>
                "This member's proxy tags are no longer hidden from other systems.",
            (MemberPrivacySubject.Metadata, PrivacyLevel.Public) =>
                "This member's metadata (eg. created timestamp, message count, etc) is no longer hidden from other systems.",
            (MemberPrivacySubject.Visibility, PrivacyLevel.Public) =>
                "This member is no longer hidden from member lists.",

            _ => throw new InvalidOperationException($"Invalid subject/level tuple ({subject}, {level})")
        };

        var replyStr = $"{Emojis.Success} {target.NameFor(ctx)}'s **{subjectName}** has been set to **{level.LevelName()}**. {explanation}";

        // Name privacy only works given a display name
        if (subject == MemberPrivacySubject.Name && level == PrivacyLevel.Private && target.DisplayName == null)
            replyStr += $"\n{Emojis.Warn} This member does not have a display name set, and name privacy **will not take effect**.";

        // Avatar privacy doesn't apply when proxying if no server avatar is set
        if (subject == MemberPrivacySubject.Avatar && level == PrivacyLevel.Private)
        {
            var guildSettings = ctx.Guild != null ? await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id) : null;
            if (guildSettings?.AvatarUrl == null)
                replyStr += $"\n{Emojis.Warn} This member does not have a server avatar set, so *proxying* will **still show the member avatar**. If you want to hide your avatar when proxying here, set a server avatar: `{ctx.DefaultPrefix}member {target.Reference(ctx)} serveravatar`";
        }

        await ctx.Reply(replyStr);
    }

    public async Task Delete(Context ctx, PKMember target)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        await ctx.Reply(
            $"{Emojis.Warn} Are you sure you want to delete \"{target.NameFor(ctx)}\"? If so, reply to this message with the member's ID (`{target.DisplayHid(ctx.Config)}`). __***This cannot be undone!***__");
        if (!await ctx.ConfirmWithReply(target.Hid, treatAsHid: true)) throw Errors.MemberDeleteCancelled;

        await ctx.Repository.DeleteMember(target.Id);

        await ctx.Reply($"{Emojis.Success} Member deleted.");
    }
}