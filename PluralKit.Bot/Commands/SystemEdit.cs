using System.Text;
using System.Text.RegularExpressions;

using Myriad.Builders;
using Myriad.Rest.Exceptions;
using Myriad.Rest.Types;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using Newtonsoft.Json;

using PluralKit.Core;
using SqlKata.Compilers;

namespace PluralKit.Bot;

public class SystemEdit
{
    private readonly HttpClient _client;
    private readonly DataFileService _dataFiles;
    private readonly PrivateChannelService _dmCache;
    private readonly AvatarHostingService _avatarHosting;

    public SystemEdit(DataFileService dataFiles, HttpClient client, PrivateChannelService dmCache, AvatarHostingService avatarHosting)
    {
        _dataFiles = dataFiles;
        _client = client;
        _dmCache = dmCache;
        _avatarHosting = avatarHosting;
    }

    public async Task ShowName(Context ctx, PKSystem target, ReplyFormat format)
    {
        ctx.CheckSystemPrivacy(target.Id, target.NamePrivacy);
        var isOwnSystem = target.Id == ctx.System?.Id;

        var noNameSetMessage = $"{(isOwnSystem ? "Your" : "This")} system does not have a name set.";
        if (isOwnSystem)
            noNameSetMessage += $" Type `{ctx.DefaultPrefix}system name <name>` to set one.";

        if (target.Name == null)
        {
            await ctx.Reply(noNameSetMessage);
            return;
        }

        if (format == ReplyFormat.Raw)
        {
            await ctx.Reply($"```\n{target.Name}\n```");
            return;
        }
        if (format == ReplyFormat.Plaintext)
        {
            var eb = new EmbedBuilder()
                .Description($"Showing name for system `{target.DisplayHid(ctx.Config)}`");
            await ctx.Reply(target.Name, embed: eb.Build());
            return;
        }

        await ctx.Reply(
            $"{(isOwnSystem ? "Your" : "This")} system's name is currently **{target.Name}**."
            + (isOwnSystem ? $" Type `{ctx.DefaultPrefix}system name -clear` to clear it."
            + $" Using {target.Name.Length}/{Limits.MaxSystemNameLength} characters." : ""));
        return;
    }

    public async Task ClearName(Context ctx, PKSystem target, bool flagConfirmYes)
    {
        ctx.CheckSystemPrivacy(target.Id, target.NamePrivacy);
        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.ConfirmClear("your system's name", flagConfirmYes))
        {
            await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { Name = null });

            await ctx.Reply($"{Emojis.Success} System name cleared.");
        }
    }

    public async Task Rename(Context ctx, PKSystem target, string newSystemName)
    {
        ctx.CheckSystemPrivacy(target.Id, target.NamePrivacy);
        ctx.CheckSystem().CheckOwnSystem(target);

        if (newSystemName.Length > Limits.MaxSystemNameLength)
            throw Errors.StringTooLongError("System name", newSystemName.Length, Limits.MaxSystemNameLength);

        await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { Name = newSystemName });

        await ctx.Reply($"{Emojis.Success} System name changed (using {newSystemName.Length}/{Limits.MaxSystemNameLength} characters).");
    }

    public async Task ShowServerName(Context ctx, PKSystem target, ReplyFormat format)
    {
        ctx.CheckGuildContext();

        var isOwnSystem = target.Id == ctx.System?.Id;

        var noNameSetMessage = $"{(isOwnSystem ? "Your" : "This")} system does not have a name specific to this server.";
        if (isOwnSystem)
            noNameSetMessage += $" Type `{ctx.DefaultPrefix}system servername <name>` to set one.";

        var settings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, target.Id);

        if (settings.DisplayName == null)
        {
            await ctx.Reply(noNameSetMessage);
            return;
        }

        if (format == ReplyFormat.Raw)
        {
            await ctx.Reply($"```\n{settings.DisplayName}\n```");
            return;
        }
        if (format == ReplyFormat.Plaintext)
        {
            var eb = new EmbedBuilder()
                .Description($"Showing servername for system `{target.DisplayHid(ctx.Config)}`");
            await ctx.Reply(settings.DisplayName, embed: eb.Build());
            return;
        }

        await ctx.Reply(
            $"{(isOwnSystem ? "Your" : "This")} system's name for this server is currently **{settings.DisplayName}**."
            + (isOwnSystem ? $" Type `{ctx.DefaultPrefix}system servername -clear` to clear it."
            + $" Using {settings.DisplayName.Length}/{Limits.MaxSystemNameLength} characters." : ""));
        return;
    }

    public async Task ClearServerName(Context ctx, PKSystem target, bool flagConfirmYes)
    {
        ctx.CheckGuildContext();
        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.ConfirmClear("your system's name for this server", flagConfirmYes))
        {
            await ctx.Repository.UpdateSystemGuild(target.Id, ctx.Guild.Id, new SystemGuildPatch { DisplayName = null });

            await ctx.Reply($"{Emojis.Success} System name for this server cleared.");
        }
    }

    public async Task RenameServerName(Context ctx, PKSystem target, string newSystemGuildName)
    {
        ctx.CheckGuildContext();
        ctx.CheckSystem().CheckOwnSystem(target);

        if (newSystemGuildName.Length > Limits.MaxSystemNameLength)
            throw Errors.StringTooLongError("System name for this server", newSystemGuildName.Length, Limits.MaxSystemNameLength);

        await ctx.Repository.UpdateSystemGuild(target.Id, ctx.Guild.Id, new SystemGuildPatch { DisplayName = newSystemGuildName });

        await ctx.Reply($"{Emojis.Success} System name for this server changed (using {newSystemGuildName.Length}/{Limits.MaxSystemNameLength} characters).");
    }

    public async Task ClearDescription(Context ctx, PKSystem target, bool flagConfirmYes)
    {
        ctx.CheckSystemPrivacy(target.Id, target.DescriptionPrivacy);
        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.ConfirmClear("your system's description", flagConfirmYes))
        {
            await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { Description = null });

            await ctx.Reply($"{Emojis.Success} System description cleared.");
        }
    }

    public async Task ShowDescription(Context ctx, PKSystem target, ReplyFormat format)
    {
        ctx.CheckSystemPrivacy(target.Id, target.DescriptionPrivacy);

        var isOwnSystem = target.Id == ctx.System?.Id;

        var noDescriptionSetMessage = "This system does not have a description set.";
        if (isOwnSystem)
            noDescriptionSetMessage += $" To set one, type `{ctx.DefaultPrefix}s description <description>`.";

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
                .Description($"Showing description for system `{target.DisplayHid(ctx.Config)}`");
            await ctx.Reply(target.Description, embed: eb.Build());
            return;
        }

        await ctx.Reply(embed: new EmbedBuilder()
            .Title("System description")
            .Description(target.Description)
            .Footer(new Embed.EmbedFooter(
                $"To print the description with formatting, type `{ctx.DefaultPrefix}s description -raw`."
                    + (isOwnSystem ? $" To clear it, type `{ctx.DefaultPrefix}s description -clear`. To change it, type `{ctx.DefaultPrefix}s description <new description>`."
                    + $" Using {target.Description.Length}/{Limits.MaxDescriptionLength} characters." : "")))
            .Build());
    }

    public async Task ChangeDescription(Context ctx, PKSystem target, string newDescription)
    {
        ctx.CheckSystemPrivacy(target.Id, target.DescriptionPrivacy);
        ctx.CheckSystem().CheckOwnSystem(target);

        newDescription = newDescription.NormalizeLineEndSpacing();
        if (newDescription.Length > Limits.MaxDescriptionLength)
            throw Errors.StringTooLongError("Description", newDescription.Length, Limits.MaxDescriptionLength);

        await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { Description = newDescription });

        await ctx.Reply($"{Emojis.Success} System description changed (using {newDescription.Length}/{Limits.MaxDescriptionLength} characters).");
    }

    public async Task ChangeColor(Context ctx, PKSystem target, string newColor)
    {
        ctx.CheckSystem().CheckOwnSystem(target);

        if (newColor.StartsWith("#")) newColor = newColor.Substring(1);
        if (!Regex.IsMatch(newColor, "^[0-9a-fA-F]{6}$")) throw Errors.InvalidColorError(newColor);

        await ctx.Repository.UpdateSystem(target.Id,
            new SystemPatch { Color = Partial<string>.Present(newColor.ToLowerInvariant()) });

        await ctx.Reply(embed: new EmbedBuilder()
            .Title($"{Emojis.Success} System color changed.")
            .Color(newColor.ToDiscordColor())
            .Thumbnail(new Embed.EmbedThumbnail($"attachment://color.gif"))
            .Build(),
            files: [MiscUtils.GenerateColorPreview(color)]););
    }

    public async Task ClearColor(Context ctx, PKSystem target, bool flagConfirmYes)
    {
        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.ConfirmClear("your system's color", flagConfirmYes))
        {
            await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { Color = Partial<string>.Null() });
            await ctx.Reply($"{Emojis.Success} System color cleared.");
        }
    }

    public async Task ShowColor(Context ctx, PKSystem target, ReplyFormat format)
    {
        var isOwnSystem = ctx.System?.Id == target.Id;

        if (target.Color == null)
        {
            await ctx.Reply(
                "This system does not have a color set." + (isOwnSystem ? $" To set one, type `{ctx.DefaultPrefix}system color <color>`." : ""));
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
            .Title("System color")
            .Color(target.Color.ToDiscordColor())
            .Thumbnail(new Embed.EmbedThumbnail($"attachment://color.gif"))
            .Description(
                $"This system's color is **#{target.Color}**." + (isOwnSystem ? $" To clear it, type `{ctx.DefaultPrefix}s color -clear`." : ""))
            .Build(),
            files: [MiscUtils.GenerateColorPreview(target.Color)]);
    }

    public async Task ClearTag(Context ctx, PKSystem target, bool flagConfirmYes)
    {
        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.ConfirmClear("your system's tag", flagConfirmYes))
        {
            await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { Tag = null });

            var replyStr = $"{Emojis.Success} System tag cleared.";

            if (ctx.Guild != null)
            {
                var servertag = (await ctx.Repository.GetSystemGuild(ctx.Guild.Id, target.Id)).Tag;
                if (servertag is not null)
                    replyStr += $"\n{Emojis.Note} You have a server tag set in this server ({servertag}) so it will still be shown on proxies.";
                else if (ctx.GuildConfig.RequireSystemTag)
                    replyStr += $"\n{Emojis.Warn} This server requires a tag in order to proxy. If you do not add a new tag you will not be able to proxy in this server.";
            }

            await ctx.Reply(replyStr);
        }
    }

    public async Task ChangeTag(Context ctx, PKSystem target, string newTag)
    {
        ctx.CheckSystem().CheckOwnSystem(target);

        newTag = newTag.NormalizeLineEndSpacing();
        if (newTag.Length > Limits.MaxSystemTagLength)
            throw Errors.StringTooLongError("System tag", newTag.Length, Limits.MaxSystemTagLength);

        await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { Tag = newTag });

        var replyStr = $"{Emojis.Success} System tag changed (using {newTag.Length}/{Limits.MaxSystemTagLength} characters).";
        if (ctx.Config.NameFormat is null || ctx.Config.NameFormat.Contains("{tag}"))
            replyStr += $"Member names will now have the tag {newTag.AsCode()} when proxied.\n{Emojis.Note}To check or change where your tag appears in your name use the command `{ctx.DefaultPrefix}cfg name format`.";
        else
            replyStr += $"\n{Emojis.Warn} You do not have a designated place for a tag in your name format so it **will not be put in proxy names**. To change this type `{ctx.DefaultPrefix}cfg name format`.";

        if (ctx.Guild != null)
        {
            var guildSettings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, target.Id);

            if (guildSettings.Tag is not null)
                replyStr += $"\n{Emojis.Note} Note that you have a server tag set ({guildSettings.Tag}) and it will be shown in proxies instead.";
            if (!guildSettings.TagEnabled)
                replyStr += $"\n{Emojis.Note} Note that your tag is disabled in this server and will not be shown in proxies. To change this type `{ctx.DefaultPrefix}system servertag enable`.";
            if (guildSettings.NameFormat is not null && !guildSettings.NameFormat.Contains("{tag}"))
                replyStr += $"\n{Emojis.Note} You do not have a designated place for a tag in your server name format so it **will not be put in proxy names**. To change this type `{ctx.DefaultPrefix}cfg server name format`.";
        }

        await ctx.Reply(replyStr);
    }

    public async Task ShowTag(Context ctx, PKSystem target, ReplyFormat format)
    {
        var isOwnSystem = ctx.System?.Id == target.Id;

        var noTagSetMessage = isOwnSystem
            ? $"You currently have no system tag set. To set one, type `{ctx.DefaultPrefix}s tag <tag>`."
            : "This system currently has no system tag set.";

        if (target.Tag == null)
        {
            await ctx.Reply(noTagSetMessage);
            return;
        }

        if (format == ReplyFormat.Raw)
        {
            await ctx.Reply($"```\n{target.Tag}\n```");
            return;
        }
        if (format == ReplyFormat.Plaintext)
        {
            var eb = new EmbedBuilder()
                .Description($"Showing tag for system `{target.DisplayHid(ctx.Config)}`");
            await ctx.Reply(target.Tag, embed: eb.Build());
            return;
        }

        await ctx.Reply($"{(isOwnSystem ? "Your" : "This system's")} current system tag is {target.Tag.AsCode()}."
            + (isOwnSystem ? $"To change it, type `{ctx.DefaultPrefix}s tag <tag>`. To clear it, type `{ctx.DefaultPrefix}s tag -clear`." : ""));
    }

    public async Task ShowServerTag(Context ctx, PKSystem target, ReplyFormat format)
    {
        ctx.CheckSystem().CheckOwnSystem(target).CheckGuildContext();

        var settings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, target.Id);

        if (settings.Tag != null)
        {
            if (format == ReplyFormat.Raw)
            {
                await ctx.Reply($"```{settings.Tag}```");
                return;
            }
            if (format == ReplyFormat.Plaintext)
            {
                var eb = new EmbedBuilder()
                    .Description($"Showing servertag for system `{target.DisplayHid(ctx.Config)}`");
                await ctx.Reply(settings.Tag, embed: eb.Build());
                return;
            }

            var msg = $"Your current system tag in '{ctx.Guild.Name}' is {settings.Tag.AsCode()}";
            if (!settings.TagEnabled)
                msg += $", but it is currently **disabled**. To re-enable it, type `{ctx.DefaultPrefix}s servertag -enable`.";
            else
                msg +=
                    $". To change it, type `{ctx.DefaultPrefix}s servertag <tag>`. To clear it, type `{ctx.DefaultPrefix}s servertag -clear`.";

            await ctx.Reply(msg);
            return;
        }

        if (!settings.TagEnabled)
            await ctx.Reply(
                $"Your global system tag is {target.Tag}, but it is **disabled** in this server. To re-enable it, type `{ctx.DefaultPrefix}s servertag -enable`");
        else
            await ctx.Reply(
                $"You currently have no system tag specific to the server '{ctx.Guild.Name}'. To set one, type `{ctx.DefaultPrefix}s servertag <tag>`. To disable the system tag in the current server, type `{ctx.DefaultPrefix}s servertag -disable`.");
    }

    public async Task ClearServerTag(Context ctx, PKSystem target, bool flagConfirmYes)
    {
        ctx.CheckSystem().CheckOwnSystem(target).CheckGuildContext();

        if (!await ctx.ConfirmClear("your system's server tag", flagConfirmYes))
            return;

        var settings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, target.Id);
        await ctx.Repository.UpdateSystemGuild(target.Id, ctx.Guild.Id, new SystemGuildPatch { Tag = null });

        var replyStr = $"{Emojis.Success} System server tag cleared. Member names will now use the global system tag, if there is one set.\n\nTo check or change where your tag appears in your name use the command `{ctx.DefaultPrefix}cfg name format`.";

        if (!settings.TagEnabled)
            replyStr += $"\n{Emojis.Warn} Your system tag is currently **disabled** in this server. No tag will be applied when proxying.\nTo re-enable the system tag in the current server, type `{ctx.DefaultPrefix}s servertag -enable`.";

        await ctx.Reply(replyStr);
    }

    public async Task ChangeServerTag(Context ctx, PKSystem target, string newTag)
    {
        ctx.CheckSystem().CheckOwnSystem(target).CheckGuildContext();

        var settings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, target.Id);

        if (newTag != null && newTag.Length > Limits.MaxSystemTagLength)
            throw Errors.StringTooLongError("System server tag", newTag.Length, Limits.MaxSystemTagLength);

        await ctx.Repository.UpdateSystemGuild(target.Id, ctx.Guild.Id, new SystemGuildPatch { Tag = newTag });

        var replyStr = $"{Emojis.Success} System server tag changed (using {newTag.Length}/{Limits.MaxSystemTagLength} characters). Member names will now have the tag {newTag.AsCode()} when proxied in the current server '{ctx.Guild.Name}'.\n\nTo check or change where your tag appears in your name use the command `{ctx.DefaultPrefix}cfg name format`.";

        if (!settings.TagEnabled)
            replyStr += $"\n{Emojis.Warn} Your system tag is currently **disabled** in this server. No tag will be applied when proxying.\nTo re-enable the system tag in the current server, type `{ctx.DefaultPrefix}s servertag -enable`.";

        await ctx.Reply(replyStr);
    }

    public async Task ToggleServerTag(Context ctx, PKSystem target, bool newValue)
    {
        ctx.CheckSystem().CheckOwnSystem(target).CheckGuildContext();

        var settings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, target.Id);
        await ctx.Repository.UpdateSystemGuild(target.Id, ctx.Guild.Id, new SystemGuildPatch { TagEnabled = newValue });

        var opStr = newValue ? "enabled" : "disabled";
        string str;

        if (newValue == settings.TagEnabled)
            str = $"{Emojis.Note} The system tag is already {opStr} in this server.";
        else
            str = $"{Emojis.Success} System tag {opStr} in this server.";

        if (newValue)
        {
            if (settings.TagEnabled)
            {
                if (settings.Tag == null)
                    str += " However, you do not have a system tag specific to this server. Messages will be proxied using your global system tag, if there is one set.";
                else
                    str += $" Your current system tag in '{ctx.Guild.Name}' is {settings.Tag.AsCode()}.";
            }
            else
            {
                if (settings.Tag != null)
                    str +=
                        $" Member names will now use the server-specific tag {settings.Tag.AsCode()} when proxied in the current server '{ctx.Guild.Name}'."
                        + $"\n\nTo check or change where your tag appears in your name use the command `{ctx.DefaultPrefix}cfg name format`.";
                else
                    str +=
                        " Member names will now use the global system tag when proxied in the current server, if there is one set."
                        + $"\n\nTo check or change where your tag appears in your name use the command `{ctx.DefaultPrefix}cfg name format`.";
            }
        }

        await ctx.Reply(str);
    }

    public async Task ClearPronouns(Context ctx, PKSystem target, bool flagConfirmYes)
    {
        ctx.CheckSystemPrivacy(target.Id, target.PronounPrivacy);
        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.ConfirmClear("your system's pronouns", flagConfirmYes))
        {
            await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { Pronouns = null });
            await ctx.Reply($"{Emojis.Success} System pronouns cleared.");
        }
    }

    public async Task ChangePronouns(Context ctx, PKSystem target, string newPronouns)
    {
        ctx.CheckSystemPrivacy(target.Id, target.PronounPrivacy);
        ctx.CheckSystem().CheckOwnSystem(target);

        newPronouns = newPronouns.NormalizeLineEndSpacing();
        if (newPronouns.Length > Limits.MaxPronounsLength)
            throw Errors.StringTooLongError("Pronouns", newPronouns.Length, Limits.MaxPronounsLength);

        await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { Pronouns = newPronouns });

        await ctx.Reply($"{Emojis.Success} System pronouns changed (using {newPronouns.Length}/{Limits.MaxPronounsLength} characters).");
    }

    public async Task ShowPronouns(Context ctx, PKSystem target, ReplyFormat format)
    {
        ctx.CheckSystemPrivacy(target.Id, target.PronounPrivacy);

        var isOwnSystem = ctx.System.Id == target.Id;

        if (target.Pronouns == null)
        {
            var noPronounsSetMessage = "This system does not have pronouns set.";
            if (isOwnSystem)
                noPronounsSetMessage += $" To set some, type `{ctx.DefaultPrefix}system pronouns <pronouns>`";

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
                .Description($"Showing pronouns for system `{target.DisplayHid(ctx.Config)}`");
            await ctx.Reply(target.Pronouns, embed: eb.Build());
            return;
        }

        await ctx.Reply($"{(isOwnSystem ? "Your" : "This system's")} current pronouns are **{target.Pronouns}**.\nTo print the pronouns with formatting, type `{ctx.DefaultPrefix}system pronouns -raw`."
            + (isOwnSystem ? $" To clear them, type `{ctx.DefaultPrefix}system pronouns -clear`."
            + $" Using {target.Pronouns.Length}/{Limits.MaxPronounsLength} characters." : ""));
    }

    public async Task ClearAvatar(Context ctx, PKSystem target, bool flagConfirmYes)
    {
        ctx.CheckSystemPrivacy(target.Id, target.AvatarPrivacy);
        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.ConfirmClear("your system's icon", flagConfirmYes))
        {
            await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { AvatarUrl = null });
            await ctx.Reply($"{Emojis.Success} System icon cleared.");
        }
    }

    public async Task ShowAvatar(Context ctx, PKSystem target, ReplyFormat format)
    {
        ctx.CheckSystemPrivacy(target.Id, target.AvatarPrivacy);
        var isOwnSystem = target.Id == ctx.System?.Id;

        if ((target.AvatarUrl?.Trim() ?? "").Length > 0)
        {
            if (!target.AvatarPrivacy.CanAccess(ctx.DirectLookupContextFor(target.Id)))
                throw new PKSyntaxError("This system does not have an icon set or it is private.");

            switch (format)
            {
                case ReplyFormat.Raw:
                    await ctx.Reply($"`{target.AvatarUrl.TryGetCleanCdnUrl()}`");
                    break;
                case ReplyFormat.Plaintext:
                    var ebP = new EmbedBuilder()
                        .Description($"Showing icon for system {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
                    await ctx.Reply(text: $"<{target.AvatarUrl.TryGetCleanCdnUrl()}>", embed: ebP.Build());
                    break;
                default:
                    var ebS = new EmbedBuilder()
                        .Title("System icon")
                        .Image(new Embed.EmbedImage(target.AvatarUrl.TryGetCleanCdnUrl()));
                    if (target.Id == ctx.System?.Id)
                        ebS.Description($"To clear, use `{ctx.DefaultPrefix}system icon clear`.");
                    await ctx.Reply(embed: ebS.Build());
                    break;
            }
        }
        else
        {
            throw new PKSyntaxError(
                $"This system does not have an icon set{(isOwnSystem ? "" : " or it is private")}."
                + (isOwnSystem ? " Set one by attaching an image to this command, or by passing an image URL or @mention." : ""));
        }
    }

    public async Task ChangeAvatar(Context ctx, PKSystem target, ParsedImage img)
    {
        ctx.CheckSystemPrivacy(target.Id, target.AvatarPrivacy);
        ctx.CheckSystem().CheckOwnSystem(target);

        img = await _avatarHosting.TryRehostImage(img, AvatarHostingService.RehostedImageType.Avatar, ctx.Author.Id, ctx.System);
        await _avatarHosting.VerifyAvatarOrThrow(img.Url);

        await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { AvatarUrl = img.CleanUrl ?? img.Url });

        var msg = img.Source switch
        {
            AvatarSource.User =>
                $"{Emojis.Success} System icon changed to {img.SourceUser?.Username}'s avatar!\n{Emojis.Warn} If {img.SourceUser?.Username} changes their avatar, the system icon will need to be re-set.",
            AvatarSource.Url => $"{Emojis.Success} System icon changed to the image at the given URL.",
            AvatarSource.HostedCdn => $"{Emojis.Success} System icon changed to attached image.",
            AvatarSource.Attachment =>
                $"{Emojis.Success} System icon changed to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the system icon will stop working.",
            _ => throw new ArgumentOutOfRangeException()
        };

        // The attachment's already right there, no need to preview it.
        var hasEmbed = img.Source != AvatarSource.Attachment && img.Source != AvatarSource.HostedCdn;
        await (hasEmbed
            ? ctx.Reply(msg, new EmbedBuilder().Image(new Embed.EmbedImage(img.Url)).Build())
            : ctx.Reply(msg));
    }

    public async Task ClearServerAvatar(Context ctx, PKSystem target, bool flagConfirmYes)
    {
        ctx.CheckGuildContext();
        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.ConfirmClear("your system's icon for this server", flagConfirmYes))
        {
            await ctx.Repository.UpdateSystemGuild(target.Id, ctx.Guild.Id, new SystemGuildPatch { AvatarUrl = null });
            await ctx.Reply($"{Emojis.Success} System server icon cleared.");
        }
    }

    public async Task ShowServerAvatar(Context ctx, PKSystem target, ReplyFormat format)
    {
        ctx.CheckGuildContext();
        var isOwnSystem = target.Id == ctx.System?.Id;

        var settings = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, target.Id);

        if ((settings.AvatarUrl?.Trim() ?? "").Length > 0)
        {
            if (!target.AvatarPrivacy.CanAccess(ctx.DirectLookupContextFor(target.Id)))
                throw new PKSyntaxError("This system does not have a icon specific to this server or it is private.");

            switch (format)
            {
                case ReplyFormat.Raw:
                    await ctx.Reply($"`{settings.AvatarUrl.TryGetCleanCdnUrl()}`");
                    break;
                case ReplyFormat.Plaintext:
                    var ebP = new EmbedBuilder()
                        .Description($"Showing icon for system {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
                    await ctx.Reply(text: $"<{settings.AvatarUrl.TryGetCleanCdnUrl()}>", embed: ebP.Build());
                    break;
                default:
                    var ebS = new EmbedBuilder()
                        .Title("System server icon")
                        .Image(new Embed.EmbedImage(settings.AvatarUrl.TryGetCleanCdnUrl()));
                    if (target.Id == ctx.System?.Id)
                        ebS.Description($"To clear, use `{ctx.DefaultPrefix}system servericon clear`.");
                    await ctx.Reply(embed: ebS.Build());
                    break;
            }
        }
        else
        {
            throw new PKSyntaxError(
                $"This system does not have a icon specific to this server{(isOwnSystem ? "" : " or it is private")}."
                + (isOwnSystem ? " Set one by attaching an image to this command, or by passing an image URL or @mention." : ""));
        }
    }

    public async Task ChangeServerAvatar(Context ctx, PKSystem target, ParsedImage img)
    {
        ctx.CheckGuildContext();
        ctx.CheckSystem().CheckOwnSystem(target);

        img = await _avatarHosting.TryRehostImage(img, AvatarHostingService.RehostedImageType.Avatar, ctx.Author.Id, ctx.System);
        await _avatarHosting.VerifyAvatarOrThrow(img.Url);

        await ctx.Repository.UpdateSystemGuild(target.Id, ctx.Guild.Id, new SystemGuildPatch { AvatarUrl = img.CleanUrl ?? img.Url });

        var msg = img.Source switch
        {
            AvatarSource.User =>
                $"{Emojis.Success} System icon for this server changed to {img.SourceUser?.Username}'s avatar! It will now be used for anything that uses system avatar in this server.\n{Emojis.Warn} If {img.SourceUser?.Username} changes their avatar, the system icon for this server will need to be re-set.",
            AvatarSource.Url =>
                $"{Emojis.Success} System icon for this server changed to the image at the given URL. It will now be used for anything that uses system avatar in this server.",
            AvatarSource.HostedCdn => $"{Emojis.Success} System icon for this server changed to attached image.",
            AvatarSource.Attachment =>
                $"{Emojis.Success} System icon for this server changed to attached image. It will now be used for anything that uses system avatar in this server.\n{Emojis.Warn} If you delete the message containing the attachment, the system icon for this server will stop working.",
            _ => throw new ArgumentOutOfRangeException()
        };

        // The attachment's already right there, no need to preview it.
        var hasEmbed = img.Source != AvatarSource.Attachment && img.Source != AvatarSource.HostedCdn;
        await (hasEmbed
            ? ctx.Reply(msg, new EmbedBuilder().Image(new Embed.EmbedImage(img.Url)).Build())
            : ctx.Reply(msg));
    }

    public async Task ClearBannerImage(Context ctx, PKSystem target, bool flagConfirmYes)
    {
        ctx.CheckSystemPrivacy(target.Id, target.BannerPrivacy);
        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.ConfirmClear("your system's banner image", flagConfirmYes))
        {
            await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { BannerImage = null });
            await ctx.Reply($"{Emojis.Success} System banner image cleared.");
        }
    }

    public async Task ShowBannerImage(Context ctx, PKSystem target, ReplyFormat format)
    {
        ctx.CheckSystemPrivacy(target.Id, target.BannerPrivacy);
        var isOwnSystem = target.Id == ctx.System?.Id;

        if ((target.BannerImage?.Trim() ?? "").Length > 0)
        {
            switch (format)
            {
                case ReplyFormat.Raw:
                    await ctx.Reply($"`{target.BannerImage.TryGetCleanCdnUrl()}`");
                    break;
                case ReplyFormat.Plaintext:
                    var ebP = new EmbedBuilder()
                        .Description($"Showing banner for system {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
                    await ctx.Reply(text: $"<{target.BannerImage.TryGetCleanCdnUrl()}>", embed: ebP.Build());
                    break;
                default:
                    var ebS = new EmbedBuilder()
                        .Title("System banner image")
                        .Image(new Embed.EmbedImage(target.BannerImage.TryGetCleanCdnUrl()));
                    if (target.Id == ctx.System?.Id)
                        ebS.Description($"To clear, use `{ctx.DefaultPrefix}system banner clear`.");
                    await ctx.Reply(embed: ebS.Build());
                    break;
            }
        }
        else
        {
            throw new PKSyntaxError("This system does not have a banner image set."
                + (isOwnSystem ? " Set one by attaching an image to this command, or by passing an image URL or @mention." : ""));
        }
    }

    public async Task ChangeBannerImage(Context ctx, PKSystem target, ParsedImage img)
    {
        ctx.CheckSystemPrivacy(target.Id, target.BannerPrivacy);
        ctx.CheckSystem().CheckOwnSystem(target);

        img = await _avatarHosting.TryRehostImage(img, AvatarHostingService.RehostedImageType.Banner, ctx.Author.Id, ctx.System);
        await _avatarHosting.VerifyAvatarOrThrow(img.Url, true);

        await ctx.Repository.UpdateSystem(target.Id, new SystemPatch { BannerImage = img.CleanUrl ?? img.Url });

        var msg = img.Source switch
        {
            AvatarSource.Url => $"{Emojis.Success} System banner image changed to the image at the given URL.",
            AvatarSource.HostedCdn => $"{Emojis.Success} System banner image changed to attached image.",
            AvatarSource.Attachment =>
                $"{Emojis.Success} System banner image changed to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the banner image will stop working.",
            AvatarSource.User => throw new PKError("Cannot set a banner image to an user's avatar."),
            _ => throw new ArgumentOutOfRangeException()
        };

        // The attachment's already right there, no need to preview it.
        var hasEmbed = img.Source != AvatarSource.Attachment && img.Source != AvatarSource.HostedCdn;
        await (hasEmbed
            ? ctx.Reply(msg, new EmbedBuilder().Image(new Embed.EmbedImage(img.Url)).Build())
            : ctx.Reply(msg));
    }

    public async Task Delete(Context ctx, PKSystem target, bool noExport)
    {
        ctx.CheckSystem().CheckOwnSystem(target);

        var warnMsg = $"{Emojis.Warn} Are you sure you want to delete your system? If so, reply to this message with your system's ID (`{target.DisplayHid(ctx.Config)}`).\n";
        if (!noExport)
            warnMsg += "**Note: this action is permanent,** but you will get a copy of your system's data that can be re-imported into PluralKit at a later date sent to you in DMs."
                + $" If you don't want this to happen, use `{ctx.DefaultPrefix}s delete -no-export` instead.";

        await ctx.Reply(warnMsg);
        if (!await ctx.ConfirmWithReply(target.Hid, treatAsHid: true))
            throw new PKError(
                $"System deletion cancelled. Note that you must reply with your system ID (`{target.DisplayHid(ctx.Config)}`) *verbatim*.");

        // If the user confirms the deletion, export their system and send them the export file before actually
        // deleting their system, unless they specifically tell us not to do an export.
        if (!noExport)
        {
            var json = await ctx.BusyIndicator(async () =>
            {
                // Make the actual data file
                var data = await _dataFiles.ExportSystem(ctx.System);
                return JsonConvert.SerializeObject(data, Formatting.None);
            });

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

            try
            {
                var dm = await _dmCache.GetOrCreateDmChannel(ctx.Author.Id);
                var msg = await ctx.Rest.CreateMessage(dm,
                    new MessageRequest { Content = $"{Emojis.Success} System deleted. If you want to set up your PluralKit system again, you can import the file below with `{ctx.DefaultPrefix}import`." },
                    new[] { new MultipartFile("system.json", stream, null, null, null) });
                await ctx.Rest.CreateMessage(dm, new MessageRequest { Content = $"<{msg.Attachments[0].Url}>" });

                // If the original message wasn't posted in DMs, send a public reminder
                if (ctx.Channel.Type != Channel.ChannelType.Dm)
                    await ctx.Reply($"{Emojis.Success} System deleted. Check your DMs for a copy of your system's data!");
            }
            catch (ForbiddenException)
            {
                // If user has DMs closed, tell 'em to open them
                throw new PKError(
                    $"I couldn't send you a DM with your system's data before deleting your system. Either make sure your DMs are open, or use `{ctx.DefaultPrefix}s delete -no-export` to delete your system without exporting first.");
            }
        }
        else
        {
            await ctx.Reply($"{Emojis.Success} System deleted.");
        }

        // Now that we've sent the export data (or been told not to), we can safely delete the system

        await ctx.Repository.DeleteSystem(target.Id);
    }

    public async Task ToggleSystemProxy(Context ctx, Guild guildArg, bool newValue)
    {
        ctx.CheckSystem();

        var guild = guildArg ??
            throw new PKError("You must run this command in a server or pass a server ID.");

        string serverText;
        if (guild.Id == ctx.Guild?.Id)
            serverText = $"this server ({guild.Name.EscapeMarkdown()})";
        else
            serverText = $"the server {guild.Name.EscapeMarkdown()}";

        await ctx.Repository.UpdateSystemGuild(ctx.System.Id, guild.Id, new SystemGuildPatch { ProxyEnabled = newValue });

        if (newValue)
            await ctx.Reply($"Message proxying in {serverText} is now **enabled** for your system.");
        else
            await ctx.Reply($"Message proxying in {serverText} is now **disabled** for your system.");
    }

    public async Task ShowSystemProxy(Context ctx, Guild guildArg)
    {
        ctx.CheckSystem();

        var guild = guildArg ??
            throw new PKError("You must run this command in a server or pass a server ID.");

        var gs = await ctx.Repository.GetSystemGuild(guild.Id, ctx.System.Id);

        string serverText;
        if (guild.Id == ctx.Guild?.Id)
            serverText = $"this server ({guild.Name.EscapeMarkdown()})";
        else
            serverText = $"the server {guild.Name.EscapeMarkdown()}";

        if (gs.ProxyEnabled)
            await ctx.Reply(
                $"Proxying in {serverText} is currently **enabled** for your system. To disable it, type `{ctx.DefaultPrefix}system proxy off`.");
        else
            await ctx.Reply(
                $"Proxying in {serverText} is currently **disabled** for your system. To enable it, type `{ctx.DefaultPrefix}system proxy on`.");
    }

    public async Task ShowSystemPrivacy(Context ctx, PKSystem target)
    {
        ctx.CheckSystem().CheckOwnSystem(target);

        var eb = new EmbedBuilder()
            .Title("Current privacy settings for your system")
            .Field(new Embed.Field("Name", target.NamePrivacy.Explanation()))
            .Field(new Embed.Field("Avatar", target.AvatarPrivacy.Explanation()))
            .Field(new Embed.Field("Description", target.DescriptionPrivacy.Explanation()))
            .Field(new Embed.Field("Banner", target.BannerPrivacy.Explanation()))
            .Field(new Embed.Field("Pronouns", target.PronounPrivacy.Explanation()))
            .Field(new Embed.Field("Member list", target.MemberListPrivacy.Explanation()))
            .Field(new Embed.Field("Group list", target.GroupListPrivacy.Explanation()))
            .Field(new Embed.Field("Current fronter(s)", target.FrontPrivacy.Explanation()))
            .Field(new Embed.Field("Front/switch history", target.FrontHistoryPrivacy.Explanation()))
            .Description(
                $"To edit privacy settings, use the command:\n`{ctx.DefaultPrefix}system privacy <subject> <level>`\n\n- `subject` is one of `name`, `avatar`, `description`, `banner`, `pronouns`, `list`, `front`, `fronthistory`, `groups`, or `all` \n- `level` is either `public` or `private`.");
        await ctx.Reply(embed: eb.Build());
    }

    public async Task ChangeSystemPrivacy(Context ctx, PKSystem target, SystemPrivacySubject subject, PrivacyLevel level)
    {
        ctx.CheckSystem().CheckOwnSystem(target);

        await ctx.Repository.UpdateSystem(target.Id, new SystemPatch().WithPrivacy(subject, level));

        var levelExplanation = level switch
        {
            PrivacyLevel.Public => "be able to query",
            PrivacyLevel.Private => "*not* be able to query",
            _ => ""
        };

        var subjectStr = subject switch
        {
            SystemPrivacySubject.Name => "name",
            SystemPrivacySubject.Avatar => "avatar",
            SystemPrivacySubject.Description => "description",
            SystemPrivacySubject.Banner => "banner",
            SystemPrivacySubject.Pronouns => "pronouns",
            SystemPrivacySubject.Front => "front",
            SystemPrivacySubject.FrontHistory => "front history",
            SystemPrivacySubject.MemberList => "member list",
            SystemPrivacySubject.GroupList => "group list",
            _ => ""
        };

        var msg = $"System {subjectStr} privacy has been set to **{level.LevelName()}**. Other accounts will now {levelExplanation} your system {subjectStr}.";
        await ctx.Reply($"{Emojis.Success} {msg}");
    }

    public async Task ChangeSystemPrivacyAll(Context ctx, PKSystem target, PrivacyLevel level)
    {
        ctx.CheckSystem().CheckOwnSystem(target);

        await ctx.Repository.UpdateSystem(target.Id, new SystemPatch().WithAllPrivacy(level));

        var msg = level switch
        {
            PrivacyLevel.Private =>
                $"All system privacy settings have been set to **{level.LevelName()}**. Other accounts will now not be able to view your member list, group list, front history, or system description.",
            PrivacyLevel.Public =>
                $"All system privacy settings have been set to **{level.LevelName()}**. Other accounts will now be able to view everything.",
            _ => ""
        };

        await ctx.Reply($"{Emojis.Success} {msg}");
    }
}