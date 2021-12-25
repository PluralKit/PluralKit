using System.Text.RegularExpressions;

using Myriad.Builders;
using Myriad.Types;

using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;

using PluralKit.Core;

namespace PluralKit.Bot;

public class SystemEdit
{
    private readonly HttpClient _client;
    private readonly ModelRepository _repo;

    public SystemEdit(ModelRepository repo, HttpClient client)
    {
        _repo = repo;
        _client = client;
    }

    public async Task Name(Context ctx, PKSystem target)
    {
        var isOwnSystem = target.Id == ctx.System?.Id;

        var noNameSetMessage = $"{(isOwnSystem ? "Your" : "This")} system does not have a name set.";
        if (isOwnSystem)
            noNameSetMessage += " Type `pk;system name <name>` to set one.";

        if (ctx.MatchRaw())
        {
            if (target.Name != null)
                await ctx.Reply($"```\n{target.Name}\n```");
            else
                await ctx.Reply(noNameSetMessage);
            return;
        }

        if (!ctx.HasNext(false))
        {
            if (target.Name != null)
                await ctx.Reply(
                    $"{(isOwnSystem ? "Your" : "This")} system's name is currently **{target.Name}**."
                    + (isOwnSystem ? " Type `pk;system name -clear` to clear it." : ""));
            else
                await ctx.Reply(noNameSetMessage);
            return;
        }

        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.MatchClear("your system's name"))
        {
            await _repo.UpdateSystem(target.Id, new SystemPatch { Name = null });

            await ctx.Reply($"{Emojis.Success} System name cleared.");
        }
        else
        {
            var newSystemName = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();

            if (newSystemName.Length > Limits.MaxSystemNameLength)
                throw Errors.StringTooLongError("System name", newSystemName.Length, Limits.MaxSystemNameLength);

            await _repo.UpdateSystem(target.Id, new SystemPatch { Name = newSystemName });

            await ctx.Reply($"{Emojis.Success} System name changed.");
        }
    }

    public async Task Description(Context ctx, PKSystem target)
    {
        ctx.CheckSystemPrivacy(target.Id, target.DescriptionPrivacy);

        var isOwnSystem = target.Id == ctx.System?.Id;

        var noDescriptionSetMessage = "This system does not have a description set.";
        if (isOwnSystem)
            noDescriptionSetMessage += " To set one, type `pk;s description <description>`.";

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
                    .Title("System description")
                    .Description(target.Description)
                    .Footer(new Embed.EmbedFooter(
                        "To print the description with formatting, type `pk;s description -raw`."
                            + (isOwnSystem ? "To clear it, type `pk;s description -clear`. To change it, type `pk;s description <new description>`." : "")))
                    .Build());
            return;
        }

        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.MatchClear("your system's description"))
        {
            await _repo.UpdateSystem(target.Id, new SystemPatch { Description = null });

            await ctx.Reply($"{Emojis.Success} System description cleared.");
        }
        else
        {
            var newDescription = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();
            if (newDescription.Length > Limits.MaxDescriptionLength)
                throw Errors.StringTooLongError("Description", newDescription.Length, Limits.MaxDescriptionLength);

            await _repo.UpdateSystem(target.Id, new SystemPatch { Description = newDescription });

            await ctx.Reply($"{Emojis.Success} System description changed.");
        }
    }

    public async Task Color(Context ctx, PKSystem target)
    {
        var isOwnSystem = ctx.System?.Id == target.Id;

        if (!ctx.HasNext())
        {
            if (target.Color == null)
                await ctx.Reply(
                    "This system does not have a color set." + (isOwnSystem ? " To set one, type `pk;system color <color>`." : ""));
            else
                await ctx.Reply(embed: new EmbedBuilder()
                    .Title("System color")
                    .Color(target.Color.ToDiscordColor())
                    .Thumbnail(new Embed.EmbedThumbnail($"https://fakeimg.pl/256x256/{target.Color}/?text=%20"))
                    .Description(
                        $"This system's color is **#{target.Color}**." + (isOwnSystem ? " To clear it, type `pk;s color -clear`." : ""))
                    .Build());
            return;
        }

        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.MatchClear())
        {
            await _repo.UpdateSystem(target.Id, new SystemPatch { Color = Partial<string>.Null() });

            await ctx.Reply($"{Emojis.Success} System color cleared.");
        }
        else
        {
            var color = ctx.RemainderOrNull();

            if (color.StartsWith("#")) color = color.Substring(1);
            if (!Regex.IsMatch(color, "^[0-9a-fA-F]{6}$")) throw Errors.InvalidColorError(color);

            await _repo.UpdateSystem(target.Id,
                new SystemPatch { Color = Partial<string>.Present(color.ToLowerInvariant()) });

            await ctx.Reply(embed: new EmbedBuilder()
                .Title($"{Emojis.Success} System color changed.")
                .Color(color.ToDiscordColor())
                .Thumbnail(new Embed.EmbedThumbnail($"https://fakeimg.pl/256x256/{color}/?text=%20"))
                .Build());
        }
    }

    public async Task Tag(Context ctx, PKSystem target)
    {
        var isOwnSystem = ctx.System?.Id == target.Id;

        var noTagSetMessage = isOwnSystem
            ? "You currently have no system tag set. To set one, type `pk;s tag <tag>`."
            : "This system currently has no system tag set.";

        if (ctx.MatchRaw())
        {
            if (target.Tag == null)
                await ctx.Reply(noTagSetMessage);
            else
                await ctx.Reply($"```\n{target.Tag}\n```");
            return;
        }

        if (!ctx.HasNext(false))
        {
            if (target.Tag == null)
                await ctx.Reply(noTagSetMessage);
            else
                await ctx.Reply($"{(isOwnSystem ? "Your" : "This system's")} current system tag is {target.Tag.AsCode()}."
                    + (isOwnSystem ? "To change it, type `pk;s tag <tag>`. To clear it, type `pk;s tag -clear`." : ""));
            return;
        }

        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.MatchClear("your system's tag"))
        {
            await _repo.UpdateSystem(target.Id, new SystemPatch { Tag = null });

            await ctx.Reply($"{Emojis.Success} System tag cleared.");
        }
        else
        {
            var newTag = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();
            if (newTag != null)
                if (newTag.Length > Limits.MaxSystemTagLength)
                    throw Errors.StringTooLongError("System tag", newTag.Length, Limits.MaxSystemTagLength);

            await _repo.UpdateSystem(target.Id, new SystemPatch { Tag = newTag });

            await ctx.Reply(
                $"{Emojis.Success} System tag changed. Member names will now end with {newTag.AsCode()} when proxied.");
        }
    }

    public async Task ServerTag(Context ctx, PKSystem target)
    {
        ctx.CheckSystem().CheckOwnSystem(target).CheckGuildContext();

        var setDisabledWarning =
            $"{Emojis.Warn} Your system tag is currently **disabled** in this server. No tag will be applied when proxying.\nTo re-enable the system tag in the current server, type `pk;s servertag -enable`.";

        var settings = await _repo.GetSystemGuild(ctx.Guild.Id, target.Id);

        async Task Show(bool raw = false)
        {
            if (settings.Tag != null)
            {
                if (raw)
                {
                    await ctx.Reply($"```{settings.Tag}```");
                    return;
                }

                var msg = $"Your current system tag in '{ctx.Guild.Name}' is {settings.Tag.AsCode()}";
                if (!settings.TagEnabled)
                    msg += ", but it is currently **disabled**. To re-enable it, type `pk;s servertag -enable`.";
                else
                    msg +=
                        ". To change it, type `pk;s servertag <tag>`. To clear it, type `pk;s servertag -clear`.";

                await ctx.Reply(msg);
                return;
            }

            if (!settings.TagEnabled)
                await ctx.Reply(
                    $"Your global system tag is {target.Tag}, but it is **disabled** in this server. To re-enable it, type `pk;s servertag -enable`");
            else
                await ctx.Reply(
                    $"You currently have no system tag specific to the server '{ctx.Guild.Name}'. To set one, type `pk;s servertag <tag>`. To disable the system tag in the current server, type `pk;s servertag -disable`.");
        }

        async Task Set()
        {
            var newTag = ctx.RemainderOrNull(false);
            if (newTag != null && newTag.Length > Limits.MaxSystemTagLength)
                throw Errors.StringTooLongError("System server tag", newTag.Length, Limits.MaxSystemTagLength);

            await _repo.UpdateSystemGuild(target.Id, ctx.Guild.Id, new SystemGuildPatch { Tag = newTag });

            await ctx.Reply(
                $"{Emojis.Success} System server tag changed. Member names will now end with {newTag.AsCode()} when proxied in the current server '{ctx.Guild.Name}'.");

            if (!ctx.MessageContext.TagEnabled)
                await ctx.Reply(setDisabledWarning);
        }

        async Task Clear()
        {
            await _repo.UpdateSystemGuild(target.Id, ctx.Guild.Id, new SystemGuildPatch { Tag = null });

            await ctx.Reply(
                $"{Emojis.Success} System server tag cleared. Member names will now end with the global system tag, if there is one set.");

            if (!ctx.MessageContext.TagEnabled)
                await ctx.Reply(setDisabledWarning);
        }

        async Task EnableDisable(bool newValue)
        {
            await _repo.UpdateSystemGuild(target.Id, ctx.Guild.Id,
                new SystemGuildPatch { TagEnabled = newValue });

            await ctx.Reply(PrintEnableDisableResult(newValue, newValue != ctx.MessageContext.TagEnabled));
        }

        string PrintEnableDisableResult(bool newValue, bool changedValue)
        {
            var opStr = newValue ? "enabled" : "disabled";
            var str = "";

            if (!changedValue)
                str = $"{Emojis.Note} The system tag is already {opStr} in this server.";
            else
                str = $"{Emojis.Success} System tag {opStr} in this server.";

            if (newValue)
            {
                if (ctx.MessageContext.TagEnabled)
                {
                    if (ctx.MessageContext.SystemGuildTag == null)
                        str +=
                            " However, you do not have a system tag specific to this server. Messages will be proxied using your global system tag, if there is one set.";
                    else
                        str +=
                            $" Your current system tag in '{ctx.Guild.Name}' is {ctx.MessageContext.SystemGuildTag.AsCode()}.";
                }
                else
                {
                    if (ctx.MessageContext.SystemGuildTag != null)
                        str +=
                            $" Member names will now end with the server-specific tag {ctx.MessageContext.SystemGuildTag.AsCode()} when proxied in the current server '{ctx.Guild.Name}'.";
                    else
                        str +=
                            " Member names will now end with the global system tag when proxied in the current server, if there is one set.";
                }
            }

            return str;
        }

        if (await ctx.MatchClear("your system's server tag"))
            await Clear();
        else if (ctx.Match("disable") || ctx.MatchFlag("disable"))
            await EnableDisable(false);
        else if (ctx.Match("enable") || ctx.MatchFlag("enable"))
            await EnableDisable(true);
        else if (ctx.MatchRaw())
            await Show(true);
        else if (!ctx.HasNext(false))
            await Show();
        else
            await Set();
    }

    public async Task Avatar(Context ctx, PKSystem target)
    {
        async Task ClearIcon()
        {
            ctx.CheckOwnSystem(target);

            await _repo.UpdateSystem(target.Id, new SystemPatch { AvatarUrl = null });
            await ctx.Reply($"{Emojis.Success} System icon cleared.");
        }

        async Task SetIcon(ParsedImage img)
        {
            ctx.CheckOwnSystem(target);

            await AvatarUtils.VerifyAvatarOrThrow(_client, img.Url);

            await _repo.UpdateSystem(target.Id, new SystemPatch { AvatarUrl = img.Url });

            var msg = img.Source switch
            {
                AvatarSource.User =>
                    $"{Emojis.Success} System icon changed to {img.SourceUser?.Username}'s avatar!\n{Emojis.Warn} If {img.SourceUser?.Username} changes their avatar, the system icon will need to be re-set.",
                AvatarSource.Url => $"{Emojis.Success} System icon changed to the image at the given URL.",
                AvatarSource.Attachment =>
                    $"{Emojis.Success} System icon changed to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the system icon will stop working.",
                _ => throw new ArgumentOutOfRangeException()
            };

            // The attachment's already right there, no need to preview it.
            var hasEmbed = img.Source != AvatarSource.Attachment;
            await (hasEmbed
                ? ctx.Reply(msg, new EmbedBuilder().Image(new Embed.EmbedImage(img.Url)).Build())
                : ctx.Reply(msg));
        }

        async Task ShowIcon()
        {
            if ((target.AvatarUrl?.Trim() ?? "").Length > 0)
            {
                var eb = new EmbedBuilder()
                    .Title("System icon")
                    .Image(new Embed.EmbedImage(target.AvatarUrl.TryGetCleanCdnUrl()));
                if (target.Id == ctx.System?.Id)
                    eb.Description("To clear, use `pk;system icon clear`.");
                await ctx.Reply(embed: eb.Build());
            }
            else
            {
                throw new PKSyntaxError(
                    "This system does not have an icon set. Set one by attaching an image to this command, or by passing an image URL or @mention.");
            }
        }

        if (target != null && target?.Id != ctx.System?.Id)
        {
            await ShowIcon();
            return;
        }

        if (await ctx.MatchClear("your system's icon"))
            await ClearIcon();
        else if (await ctx.MatchImage() is { } img)
            await SetIcon(img);
        else
            await ShowIcon();
    }

    public async Task BannerImage(Context ctx, PKSystem target)
    {
        ctx.CheckSystemPrivacy(target.Id, target.DescriptionPrivacy);

        var isOwnSystem = target.Id == ctx.System?.Id;

        if (!ctx.HasNext() && ctx.Message.Attachments.Length == 0)
        {
            if ((target.BannerImage?.Trim() ?? "").Length > 0)
            {
                var eb = new EmbedBuilder()
                    .Title("System banner image")
                    .Image(new Embed.EmbedImage(target.BannerImage));

                if (isOwnSystem)
                    eb.Description("To clear, use `pk;system banner clear`.");

                await ctx.Reply(embed: eb.Build());
            }
            else
            {
                throw new PKSyntaxError(
                    "This system does not have a banner image set." + (isOwnSystem ? "Set one by attaching an image to this command, or by passing an image URL or @mention." : ""));
            }
            return;
        }

        ctx.CheckSystem().CheckOwnSystem(target);

        if (await ctx.MatchClear("your system's banner image"))
        {
            await _repo.UpdateSystem(target.Id, new SystemPatch { BannerImage = null });
            await ctx.Reply($"{Emojis.Success} System banner image cleared.");
        }

        else if (await ctx.MatchImage() is { } img)
        {
            await AvatarUtils.VerifyAvatarOrThrow(_client, img.Url, true);

            await _repo.UpdateSystem(target.Id, new SystemPatch { BannerImage = img.Url });

            var msg = img.Source switch
            {
                AvatarSource.Url => $"{Emojis.Success} System banner image changed to the image at the given URL.",
                AvatarSource.Attachment =>
                    $"{Emojis.Success} System banner image changed to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the banner image will stop working.",
                AvatarSource.User => throw new PKError("Cannot set a banner image to an user's avatar."),
                _ => throw new ArgumentOutOfRangeException()
            };

            // The attachment's already right there, no need to preview it.
            var hasEmbed = img.Source != AvatarSource.Attachment;
            await (hasEmbed
                ? ctx.Reply(msg, new EmbedBuilder().Image(new Embed.EmbedImage(img.Url)).Build())
                : ctx.Reply(msg));
        }

    }

    public async Task Delete(Context ctx, PKSystem target)
    {
        ctx.CheckSystem().CheckOwnSystem(target);

        await ctx.Reply(
            $"{Emojis.Warn} Are you sure you want to delete your system? If so, reply to this message with your system's ID (`{target.Hid}`).\n**Note: this action is permanent.**");
        if (!await ctx.ConfirmWithReply(target.Hid))
            throw new PKError(
                $"System deletion cancelled. Note that you must reply with your system ID (`{target.Hid}`) *verbatim*.");

        await _repo.DeleteSystem(target.Id);

        await ctx.Reply($"{Emojis.Success} System deleted.");
    }

    public async Task SystemProxy(Context ctx)
    {
        ctx.CheckSystem();

        var guild = await ctx.MatchGuild() ?? ctx.Guild ??
            throw new PKError("You must run this command in a server or pass a server ID.");

        var gs = await _repo.GetSystemGuild(guild.Id, ctx.System.Id);

        string serverText;
        if (guild.Id == ctx.Guild?.Id)
            serverText = $"this server ({guild.Name.EscapeMarkdown()})";
        else
            serverText = $"the server {guild.Name.EscapeMarkdown()}";

        bool newValue;
        // todo: MatchToggle
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
            if (gs.ProxyEnabled)
                await ctx.Reply(
                    $"Proxying in {serverText} is currently **enabled** for your system. To disable it, type `pk;system proxy off`.");
            else
                await ctx.Reply(
                    $"Proxying in {serverText} is currently **disabled** for your system. To enable it, type `pk;system proxy on`.");
            return;
        }

        await _repo.UpdateSystemGuild(ctx.System.Id, guild.Id, new SystemGuildPatch { ProxyEnabled = newValue });

        if (newValue)
            await ctx.Reply($"Message proxying in {serverText} is now **enabled** for your system.");
        else
            await ctx.Reply($"Message proxying in {serverText} is now **disabled** for your system.");
    }

    public async Task SystemPrivacy(Context ctx, PKSystem target)
    {
        ctx.CheckSystem().CheckOwnSystem(target);

        Task PrintEmbed()
        {
            var eb = new EmbedBuilder()
                .Title("Current privacy settings for your system")
                .Field(new Embed.Field("Description", target.DescriptionPrivacy.Explanation()))
                .Field(new Embed.Field("Member list", target.MemberListPrivacy.Explanation()))
                .Field(new Embed.Field("Group list", target.GroupListPrivacy.Explanation()))
                .Field(new Embed.Field("Current fronter(s)", target.FrontPrivacy.Explanation()))
                .Field(new Embed.Field("Front/switch history", target.FrontHistoryPrivacy.Explanation()))
                .Description(
                    "To edit privacy settings, use the command:\n`pk;system privacy <subject> <level>`\n\n- `subject` is one of `description`, `list`, `front`, `fronthistory`, `groups`, or `all` \n- `level` is either `public` or `private`.");
            return ctx.Reply(embed: eb.Build());
        }

        async Task SetLevel(SystemPrivacySubject subject, PrivacyLevel level)
        {
            await _repo.UpdateSystem(target.Id, new SystemPatch().WithPrivacy(subject, level));

            var levelExplanation = level switch
            {
                PrivacyLevel.Public => "be able to query",
                PrivacyLevel.Private => "*not* be able to query",
                _ => ""
            };

            var subjectStr = subject switch
            {
                SystemPrivacySubject.Description => "description",
                SystemPrivacySubject.Front => "front",
                SystemPrivacySubject.FrontHistory => "front history",
                SystemPrivacySubject.MemberList => "member list",
                SystemPrivacySubject.GroupList => "group list",
                _ => ""
            };

            var msg =
                $"System {subjectStr} privacy has been set to **{level.LevelName()}**. Other accounts will now {levelExplanation} your system {subjectStr}.";
            await ctx.Reply($"{Emojis.Success} {msg}");
        }

        async Task SetAll(PrivacyLevel level)
        {
            await _repo.UpdateSystem(target.Id, new SystemPatch().WithAllPrivacy(level));

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

        if (!ctx.HasNext())
            await PrintEmbed();
        else if (ctx.Match("all"))
            await SetAll(ctx.PopPrivacyLevel());
        else
            await SetLevel(ctx.PopSystemPrivacySubject(), ctx.PopPrivacyLevel());
    }
}