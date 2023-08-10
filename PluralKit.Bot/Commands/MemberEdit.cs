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

    public MemberEdit(HttpClient client)
    {
        _client = client;
    }

    public async Task Name(Context ctx, PKMember target)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        var newName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a new name for the member.");

        // Hard name length cap
        if (newName.Length > Limits.MaxMemberNameLength)
            throw Errors.StringTooLongError("Member name", newName.Length, Limits.MaxMemberNameLength);

        // Warn if there's already a member by this name
        var existingMember = await ctx.Repository.GetMemberByName(ctx.System.Id, newName);
        if (existingMember != null && existingMember.Id != target.Id)
        {
            var msg =
                $"{Emojis.Warn} You already have a member in your system with the name \"{existingMember.NameFor(ctx)}\" (`{existingMember.Hid}`). Do you want to rename this member to that name too?";
            if (!await ctx.PromptYesNo(msg, "Rename")) throw new PKError("Member renaming cancelled.");
        }

        // Rename the member
        var patch = new MemberPatch { Name = Partial<string>.Present(newName) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        await ctx.Reply($"{Emojis.Success} Member renamed (using {newName.Length}/{Limits.MaxMemberNameLength} characters).");
        if (newName.Contains(" "))
            await ctx.Reply(
                $"{Emojis.Note} Note that this member's name now contains spaces. You will need to surround it with \"double quotes\" when using commands referring to it.");
        if (target.DisplayName != null)
            await ctx.Reply(
                $"{Emojis.Note} Note that this member has a display name set ({target.DisplayName}), and will be proxied using that name instead.");

        if (ctx.Guild != null)
        {
            var memberGuildConfig = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);
            if (memberGuildConfig.DisplayName != null)
                await ctx.Reply(
                    $"{Emojis.Note} Note that this member has a server name set ({memberGuildConfig.DisplayName}) in this server ({ctx.Guild.Name}), and will be proxied using that name here.");
        }
    }

    public async Task Description(Context ctx, PKMember target)
    {
        ctx.CheckSystemPrivacy(target.System, target.DescriptionPrivacy);

        var noDescriptionSetMessage = "This member does not have a description set.";
        if (ctx.System?.Id == target.System)
            noDescriptionSetMessage +=
                $" To set one, type `pk;member {target.Reference(ctx)} description <description>`.";

        if (ctx.MatchRaw())
        {
            if (target.Description == null)
                await ctx.Reply(noDescriptionSetMessage);
            else
                await ctx.Reply($"```\n{target.Description}\n```");
            return;
        }

        if (!ctx.HasNext(false))
        {
            if (target.Description == null)
                await ctx.Reply(noDescriptionSetMessage);
            else
                await ctx.Reply(embed: new EmbedBuilder()
                    .Title("Member description")
                    .Description(target.Description)
                    .Field(new Embed.Field("\u200B",
                        $"To print the description with formatting, type `pk;member {target.Reference(ctx)} description -raw`."
                        + (ctx.System?.Id == target.System
                            ? $" To clear it, type `pk;member {target.Reference(ctx)} description -clear`."
                            : "")
                        + $" Using {target.Description.Length}/{Limits.MaxDescriptionLength} characters."))
                    .Build());
            return;
        }

        ctx.CheckOwnMember(target);

        if (ctx.MatchClear() && await ctx.ConfirmClear("this member's description"))
        {
            var patch = new MemberPatch { Description = Partial<string>.Null() };
            await ctx.Repository.UpdateMember(target.Id, patch);
            await ctx.Reply($"{Emojis.Success} Member description cleared.");
        }
        else
        {
            var description = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();
            if (description.IsLongerThan(Limits.MaxDescriptionLength))
                throw Errors.StringTooLongError("Description", description.Length, Limits.MaxDescriptionLength);

            var patch = new MemberPatch { Description = Partial<string>.Present(description) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Member description changed (using {description.Length}/{Limits.MaxDescriptionLength} characters).");
        }
    }

    public async Task Pronouns(Context ctx, PKMember target)
    {
        var noPronounsSetMessage = "This member does not have pronouns set.";
        if (ctx.System?.Id == target.System)
            noPronounsSetMessage += $"To set some, type `pk;member {target.Reference(ctx)} pronouns <pronouns>`.";

        ctx.CheckSystemPrivacy(target.System, target.PronounPrivacy);

        if (ctx.MatchRaw())
        {
            if (target.Pronouns == null)
                await ctx.Reply(noPronounsSetMessage);
            else
                await ctx.Reply($"```\n{target.Pronouns}\n```");
            return;
        }

        if (!ctx.HasNext(false))
        {
            if (target.Pronouns == null)
                await ctx.Reply(noPronounsSetMessage);
            else
                await ctx.Reply(
                    $"**{target.NameFor(ctx)}**'s pronouns are **{target.Pronouns}**.\nTo print the pronouns with formatting, type `pk;member {target.Reference(ctx)} pronouns -raw`."
                    + (ctx.System?.Id == target.System
                        ? $" To clear them, type `pk;member {target.Reference(ctx)} pronouns -clear`."
                        : "")
                    + $" Using {target.Pronouns.Length}/{Limits.MaxPronounsLength} characters.");
            return;
        }

        ctx.CheckOwnMember(target);

        if (ctx.MatchClear() && await ctx.ConfirmClear("this member's pronouns"))
        {
            var patch = new MemberPatch { Pronouns = Partial<string>.Null() };
            await ctx.Repository.UpdateMember(target.Id, patch);
            await ctx.Reply($"{Emojis.Success} Member pronouns cleared.");
        }
        else
        {
            var pronouns = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();
            if (pronouns.IsLongerThan(Limits.MaxPronounsLength))
                throw Errors.StringTooLongError("Pronouns", pronouns.Length, Limits.MaxPronounsLength);

            var patch = new MemberPatch { Pronouns = Partial<string>.Present(pronouns) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Member pronouns changed (using {pronouns.Length}/{Limits.MaxPronounsLength} characters).");
        }
    }

    public async Task BannerImage(Context ctx, PKMember target)
    {
        ctx.CheckOwnMember(target);

        async Task ClearBannerImage()
        {
            await ctx.Repository.UpdateMember(target.Id, new MemberPatch { BannerImage = null });
            await ctx.Reply($"{Emojis.Success} Member banner image cleared.");
        }

        async Task SetBannerImage(ParsedImage img)
        {
            await AvatarUtils.VerifyAvatarOrThrow(_client, img.Url, true);

            await ctx.Repository.UpdateMember(target.Id, new MemberPatch { BannerImage = img.Url });

            var msg = img.Source switch
            {
                AvatarSource.Url => $"{Emojis.Success} Member banner image changed to the image at the given URL.",
                AvatarSource.Attachment =>
                    $"{Emojis.Success} Member banner image changed to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the banner image will stop working.",
                AvatarSource.User => throw new PKError("Cannot set a banner image to an user's avatar."),
                _ => throw new ArgumentOutOfRangeException()
            };

            // The attachment's already right there, no need to preview it.
            var hasEmbed = img.Source != AvatarSource.Attachment;
            await (hasEmbed
                ? ctx.Reply(msg, new EmbedBuilder().Image(new Embed.EmbedImage(img.Url)).Build())
                : ctx.Reply(msg));
        }

        async Task ShowBannerImage()
        {
            if ((target.BannerImage?.Trim() ?? "").Length > 0)
            {
                var eb = new EmbedBuilder()
                    .Title($"{target.NameFor(ctx)}'s banner image")
                    .Image(new Embed.EmbedImage(target.BannerImage))
                    .Description($"To clear, use `pk;member {target.Hid} banner clear`.");
                await ctx.Reply(embed: eb.Build());
            }
            else
            {
                throw new PKSyntaxError(
                    "This member does not have a banner image set. Set one by attaching an image to this command, or by passing an image URL or @mention.");
            }
        }

        if (ctx.MatchClear() && await ctx.ConfirmClear("this member's banner image"))
            await ClearBannerImage();
        else if (await ctx.MatchImage() is { } img)
            await SetBannerImage(img);
        else
            await ShowBannerImage();
    }

    public async Task Color(Context ctx, PKMember target)
    {
        var isOwnSystem = ctx.System?.Id == target.System;
        var matchedRaw = ctx.MatchRaw();
        var matchedClear = ctx.MatchClear();

        if (!isOwnSystem || !(ctx.HasNext() || matchedClear))
        {
            if (target.Color == null)
                await ctx.Reply(
                    "This member does not have a color set." + (isOwnSystem ? $" To set one, type `pk;member {target.Reference(ctx)} color <color>`." : ""));
            else if (matchedRaw)
                await ctx.Reply("```\n#" + target.Color + "\n```");
            else
                await ctx.Reply(embed: new EmbedBuilder()
                    .Title("Member color")
                    .Color(target.Color.ToDiscordColor())
                    .Thumbnail(new Embed.EmbedThumbnail($"https://fakeimg.pl/256x256/{target.Color}/?text=%20"))
                    .Description($"This member's color is **#{target.Color}**."
                        + (isOwnSystem ? $" To clear it, type `pk;member {target.Reference(ctx)} color -clear`." : ""))
                    .Build());
            return;
        }

        ctx.CheckSystem().CheckOwnMember(target);

        if (matchedClear)
        {
            await ctx.Repository.UpdateMember(target.Id, new() { Color = Partial<string>.Null() });

            await ctx.Reply($"{Emojis.Success} Member color cleared.");
        }
        else
        {
            var color = ctx.RemainderOrNull();

            if (color.StartsWith("#")) color = color.Substring(1);
            if (!Regex.IsMatch(color, "^[0-9a-fA-F]{6}$")) throw Errors.InvalidColorError(color);

            var patch = new MemberPatch { Color = Partial<string>.Present(color.ToLowerInvariant()) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply(embed: new EmbedBuilder()
                .Title($"{Emojis.Success} Member color changed.")
                .Color(color.ToDiscordColor())
                .Thumbnail(new Embed.EmbedThumbnail($"https://fakeimg.pl/256x256/{color}/?text=%20"))
                .Build());
        }
    }

    public async Task Birthday(Context ctx, PKMember target)
    {
        if (ctx.MatchClear() && await ctx.ConfirmClear("this member's birthday"))
        {
            ctx.CheckOwnMember(target);

            var patch = new MemberPatch { Birthday = Partial<LocalDate?>.Null() };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Member birthdate cleared.");
        }
        else if (!ctx.HasNext())
        {
            ctx.CheckSystemPrivacy(target.System, target.BirthdayPrivacy);

            if (target.Birthday == null)
                await ctx.Reply("This member does not have a birthdate set."
                                + (ctx.System?.Id == target.System
                                    ? $" To set one, type `pk;member {target.Reference(ctx)} birthdate <birthdate>`."
                                    : ""));
            else
                await ctx.Reply($"This member's birthdate is **{target.BirthdayString}**."
                                + (ctx.System?.Id == target.System
                                    ? $" To clear it, type `pk;member {target.Reference(ctx)} birthdate -clear`."
                                    : ""));
        }
        else
        {
            ctx.CheckOwnMember(target);

            var birthdayStr = ctx.RemainderOrNull();

            LocalDate? birthday;
            if (birthdayStr == "today" || birthdayStr == "now")
                birthday = SystemClock.Instance.InZone(ctx.Zone).GetCurrentDate();
            else
                birthday = DateUtils.ParseDate(birthdayStr, true);

            if (birthday == null) throw Errors.BirthdayParseError(birthdayStr);

            var patch = new MemberPatch { Birthday = Partial<LocalDate?>.Present(birthday) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Member birthdate changed.");
        }
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
                $"Member ID: {target.Hid} | Active name in bold. Server name overrides display name, which overrides base name."
                + (target.DisplayName != null && ctx.System?.Id == target.System ? $" Using {target.DisplayName.Length}/{Limits.MaxMemberNameLength} characters for the display name." : "")
                + (memberGuildConfig?.DisplayName != null ? $" Using {memberGuildConfig?.DisplayName.Length}/{Limits.MaxMemberNameLength} characters for the server name." : "")));

        var showDisplayName = target.NamePrivacy.CanAccess(lcx);

        eb.Field(new Embed.Field("Name", boldIf(
            target.NameFor(ctx),
            (!showDisplayName || target.DisplayName == null) && memberGuildConfig?.DisplayName == null
        )));

        eb.Field(new Embed.Field("Display name", (target.DisplayName != null && showDisplayName)
            ? boldIf(target.DisplayName, memberGuildConfig?.DisplayName == null)
            : "*(none)*"
        ));

        if (ctx.Guild != null)
            eb.Field(new Embed.Field($"Server Name (in {ctx.Guild.Name})",
                memberGuildConfig?.DisplayName != null
                    ? $"**{memberGuildConfig.DisplayName}**"
                    : "*(none)*"
            ));

        return eb;
    }

    public async Task DisplayName(Context ctx, PKMember target)
    {
        async Task PrintSuccess(string text)
        {
            var successStr = text;
            if (ctx.Guild != null)
            {
                var memberGuildConfig = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);
                if (memberGuildConfig.DisplayName != null)
                    successStr +=
                        $" However, this member has a server name set in this server ({ctx.Guild.Name}), and will be proxied using that name, \"{memberGuildConfig.DisplayName}\", here.";
            }

            await ctx.Reply(successStr);
        }

        var noDisplayNameSetMessage = "This member does not have a display name set.";
        if (ctx.System?.Id == target.System)
            noDisplayNameSetMessage +=
                $" To set one, type `pk;member {target.Reference(ctx)} displayname <display name>`.";

        // No perms check, display name isn't covered by member privacy

        if (ctx.MatchRaw())
        {
            if (target.DisplayName == null)
                await ctx.Reply(noDisplayNameSetMessage);
            else
                await ctx.Reply($"```\n{target.DisplayName}\n```");
            return;
        }

        if (!ctx.HasNext(false))
        {
            var eb = await CreateMemberNameInfoEmbed(ctx, target);
            var reference = target.Reference(ctx);
            if (ctx.System?.Id == target.System)
                eb.Description(
                    $"To change display name, type `pk;member {reference} displayname <display name>`.\n"
                    + $"To clear it, type `pk;member {reference} displayname -clear`.\n"
                    + $"To print the raw display name, type `pk;member {reference} displayname -raw`.");
            await ctx.Reply(embed: eb.Build());
            return;
        }

        ctx.CheckOwnMember(target);

        if (ctx.MatchClear() && await ctx.ConfirmClear("this member's display name"))
        {
            var patch = new MemberPatch { DisplayName = Partial<string>.Null() };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await PrintSuccess(
                $"{Emojis.Success} Member display name cleared. This member will now be proxied using their member name \"{target.Name}\".");

            if (target.NamePrivacy == PrivacyLevel.Private)
                await ctx.Reply($"{Emojis.Warn} Since this member no longer has a display name set, their name privacy **can no longer take effect**.");
        }
        else
        {
            var newDisplayName = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();

            if (newDisplayName.Length > Limits.MaxMemberNameLength)
                throw Errors.StringTooLongError("Member display name", newDisplayName.Length, Limits.MaxMemberNameLength);

            var patch = new MemberPatch { DisplayName = Partial<string>.Present(newDisplayName) };
            await ctx.Repository.UpdateMember(target.Id, patch);

            await PrintSuccess(
                $"{Emojis.Success} Member display name changed (using {newDisplayName.Length}/{Limits.MaxMemberNameLength} characters). This member will now be proxied using the name \"{newDisplayName}\".");
        }
    }

    public async Task ServerName(Context ctx, PKMember target)
    {
        ctx.CheckGuildContext();

        var noServerNameSetMessage = "This member does not have a server name set.";
        if (ctx.System?.Id == target.System)
            noServerNameSetMessage +=
                $" To set one, type `pk;member {target.Reference(ctx)} servername <server name>`.";

        // No perms check, display name isn't covered by member privacy
        var memberGuildConfig = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);

        if (ctx.MatchRaw())
        {
            if (memberGuildConfig.DisplayName == null)
                await ctx.Reply(noServerNameSetMessage);
            else
                await ctx.Reply($"```\n{memberGuildConfig.DisplayName}\n```");
            return;
        }

        if (!ctx.HasNext(false))
        {
            var eb = await CreateMemberNameInfoEmbed(ctx, target);
            var reference = target.Reference(ctx);
            if (ctx.System?.Id == target.System)
                eb.Description(
                    $"To change server name, type `pk;member {reference} servername <server name>`.\nTo clear it, type `pk;member {reference} servername -clear`.\nTo print the raw server name, type `pk;member {reference} servername -raw`.");
            await ctx.Reply(embed: eb.Build());
            return;
        }

        ctx.CheckOwnMember(target);

        if (ctx.MatchClear() && await ctx.ConfirmClear("this member's server name"))
        {
            await ctx.Repository.UpdateMemberGuild(target.Id, ctx.Guild.Id, new MemberGuildPatch { DisplayName = null });

            if (target.DisplayName != null)
                await ctx.Reply(
                    $"{Emojis.Success} Member server name cleared. This member will now be proxied using their global display name \"{target.DisplayName}\" in this server ({ctx.Guild.Name}).");
            else
                await ctx.Reply(
                    $"{Emojis.Success} Member server name cleared. This member will now be proxied using their member name \"{target.NameFor(ctx)}\" in this server ({ctx.Guild.Name}).");
        }
        else
        {
            var newServerName = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();

            await ctx.Repository.UpdateMemberGuild(target.Id, ctx.Guild.Id,
                new MemberGuildPatch { DisplayName = newServerName });

            await ctx.Reply(
                $"{Emojis.Success} Member server name changed (using {newServerName.Length}/{Limits.MaxMemberNameLength} characters). This member will now be proxied using the name \"{newServerName}\" in this server ({ctx.Guild.Name}).");
        }
    }

    public async Task KeepProxy(Context ctx, PKMember target)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        bool newValue;
        if (ctx.Match("on", "enabled", "true", "yes"))
        {
            newValue = true;
        }
        else if (ctx.Match("off", "disabled", "false", "no"))
        {
            newValue = false;
        }
        else if (ctx.HasNext())
        {
            throw new PKSyntaxError("You must pass either \"on\" or \"off\".");
        }
        else
        {
            if (target.KeepProxy)
                await ctx.Reply(
                    "This member has keepproxy **enabled**, which means proxy tags will be **included** in the resulting message when proxying.");
            else
                await ctx.Reply(
                    "This member has keepproxy **disabled**, which means proxy tags will **not** be included in the resulting message when proxying.");
            return;
        }

        ;

        var patch = new MemberPatch { KeepProxy = Partial<bool>.Present(newValue) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        if (newValue)
            await ctx.Reply(
                $"{Emojis.Success} Member proxy tags will now be included in the resulting message when proxying.");
        else
            await ctx.Reply(
                $"{Emojis.Success} Member proxy tags will now not be included in the resulting message when proxying.");
    }

    public async Task Tts(Context ctx, PKMember target)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        bool newValue;
        if (ctx.Match("on", "enabled", "true", "yes"))
        {
            newValue = true;
        }
        else if (ctx.Match("off", "disabled", "false", "no"))
        {
            newValue = false;
        }
        else if (ctx.HasNext())
        {
            throw new PKSyntaxError("You must pass either \"on\" or \"off\".");
        }
        else
        {
            if (target.Tts)
                await ctx.Reply(
                    "This member has text-to-speech **enabled**, which means their messages **will be** sent as text-to-speech messages.");
            else
                await ctx.Reply(
                    "This member has text-to-speech **disabled**, which means their messages **will not** be sent as text-to-speech messages.");
            return;
        }

        ;

        var patch = new MemberPatch { Tts = Partial<bool>.Present(newValue) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        if (newValue)
            await ctx.Reply(
                $"{Emojis.Success} Member's messages will now be sent as text-to-speech messages.");
        else
            await ctx.Reply(
                $"{Emojis.Success} Member messages will no longer be sent as text-to-speech messages.");
    }

    public async Task MemberAutoproxy(Context ctx, PKMember target)
    {
        if (ctx.System == null) throw Errors.NoSystemError;
        if (target.System != ctx.System.Id) throw Errors.NotOwnMemberError;

        if (!ctx.HasNext())
        {
            if (target.AllowAutoproxy)
                await ctx.Reply(
                    "Latch/front autoproxy are **enabled** for this member. This member will be automatically proxied when autoproxy is set to latch or front mode.");
            else
                await ctx.Reply(
                    "Latch/front autoproxy are **disabled** for this member. This member will not be automatically proxied when autoproxy is set to latch or front mode.");
            return;
        }

        var newValue = ctx.MatchToggle();

        var patch = new MemberPatch { AllowAutoproxy = Partial<bool>.Present(newValue) };
        await ctx.Repository.UpdateMember(target.Id, patch);

        if (newValue)
            await ctx.Reply($"{Emojis.Success} Latch / front autoproxy have been **enabled** for this member.");
        else
            await ctx.Reply($"{Emojis.Success} Latch / front autoproxy have been **disabled** for this member.");
    }

    public async Task Privacy(Context ctx, PKMember target, PrivacyLevel? newValueFromCommand)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        // Display privacy settings
        if (!ctx.HasNext() && newValueFromCommand == null)
        {
            await ctx.Reply(embed: new EmbedBuilder()
                .Title($"Current privacy settings for {target.NameFor(ctx)}")
                .Field(new Embed.Field("Name (replaces name with display name if member has one)",
                    target.NamePrivacy.Explanation()))
                .Field(new Embed.Field("Description", target.DescriptionPrivacy.Explanation()))
                .Field(new Embed.Field("Avatar", target.AvatarPrivacy.Explanation()))
                .Field(new Embed.Field("Birthday", target.BirthdayPrivacy.Explanation()))
                .Field(new Embed.Field("Pronouns", target.PronounPrivacy.Explanation()))
                .Field(new Embed.Field("Proxy Tags", target.ProxyPrivacy.Explanation()))
                .Field(new Embed.Field("Meta (creation date, message count, last front, last message)",
                    target.MetadataPrivacy.Explanation()))
                .Field(new Embed.Field("Visibility", target.MemberVisibility.Explanation()))
                .Description(
                    "To edit privacy settings, use the command:\n`pk;member <member> privacy <subject> <level>`\n\n- `subject` is one of `name`, `description`, `avatar`, `birthday`, `pronouns`, `proxies`, `metadata`, `visibility`, or `all`\n- `level` is either `public` or `private`.")
                .Build());
            return;
        }

        // Get guild settings (mostly for warnings and such)
        MemberGuildSettings guildSettings = null;
        if (ctx.Guild != null)
            guildSettings = await ctx.Repository.GetMemberGuild(ctx.Guild.Id, target.Id);

        async Task SetAll(PrivacyLevel level)
        {
            await ctx.Repository.UpdateMember(target.Id, new MemberPatch().WithAllPrivacy(level));

            if (level == PrivacyLevel.Private)
                await ctx.Reply(
                    $"{Emojis.Success} All {target.NameFor(ctx)}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see nothing on the member card.");
            else
                await ctx.Reply(
                    $"{Emojis.Success} All {target.NameFor(ctx)}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see everything on the member card.");
        }

        async Task SetLevel(MemberPrivacySubject subject, PrivacyLevel level)
        {
            await ctx.Repository.UpdateMember(target.Id, new MemberPatch().WithPrivacy(subject, level));

            var subjectName = subject switch
            {
                MemberPrivacySubject.Name => "name privacy",
                MemberPrivacySubject.Description => "description privacy",
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

            await ctx.Reply(
                $"{Emojis.Success} {target.NameFor(ctx)}'s **{subjectName}** has been set to **{level.LevelName()}**. {explanation}");

            // Name privacy only works given a display name
            if (subject == MemberPrivacySubject.Name && level == PrivacyLevel.Private && target.DisplayName == null)
                await ctx.Reply(
                    $"{Emojis.Warn} This member does not have a display name set, and name privacy **will not take effect**.");

            // Avatar privacy doesn't apply when proxying if no server avatar is set
            if (subject == MemberPrivacySubject.Avatar && level == PrivacyLevel.Private &&
                guildSettings?.AvatarUrl == null)
                await ctx.Reply(
                    $"{Emojis.Warn} This member does not have a server avatar set, so *proxying* will **still show the member avatar**. If you want to hide your avatar when proxying here, set a server avatar: `pk;member {target.Reference(ctx)} serveravatar`");
        }

        if (ctx.Match("all") || newValueFromCommand != null)
            await SetAll(newValueFromCommand ?? ctx.PopPrivacyLevel());
        else
            await SetLevel(ctx.PopMemberPrivacySubject(), ctx.PopPrivacyLevel());
    }

    public async Task Delete(Context ctx, PKMember target)
    {
        ctx.CheckSystem().CheckOwnMember(target);

        await ctx.Reply(
            $"{Emojis.Warn} Are you sure you want to delete \"{target.NameFor(ctx)}\"? If so, reply to this message with the member's ID (`{target.Hid}`). __***This cannot be undone!***__");
        if (!await ctx.ConfirmWithReply(target.Hid)) throw Errors.MemberDeleteCancelled;

        await ctx.Repository.DeleteMember(target.Id);

        await ctx.Reply($"{Emojis.Success} Member deleted.");
    }
}