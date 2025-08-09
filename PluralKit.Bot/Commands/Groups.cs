using System.Text;
using System.Text.RegularExpressions;

using Myriad.Builders;
using Myriad.Types;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.Bot;

public class Groups
{
    public enum AddRemoveOperation
    {
        Add,
        Remove
    }

    private readonly HttpClient _client;
    private readonly DispatchService _dispatch;
    private readonly EmbedService _embeds;
    private readonly AvatarHostingService _avatarHosting;

    public Groups(EmbedService embeds, HttpClient client,
                  DispatchService dispatch, AvatarHostingService avatarHosting)
    {
        _embeds = embeds;
        _client = client;
        _dispatch = dispatch;
        _avatarHosting = avatarHosting;
    }

    public async Task CreateGroup(Context ctx)
    {
        ctx.CheckSystem();

        // Check group name length
        var groupName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a group name.");
        if (groupName.Length > Limits.MaxGroupNameLength)
            throw new PKError($"Group name too long ({groupName.Length}/{Limits.MaxGroupNameLength} characters).");

        // Check group cap
        var existingGroupCount = await ctx.Repository.GetSystemGroupCount(ctx.System.Id);
        var groupLimit = ctx.Config.GroupLimitOverride ?? Limits.MaxGroupCount;
        if (existingGroupCount >= groupLimit)
            throw new PKError(
                $"System has reached the maximum number of groups ({groupLimit}). If you need to add more groups, you can either delete existing groups, or ask for your limit to be raised in the PluralKit support server: <https://discord.gg/PczBt78>");

        // Warn if there's already a group by this name
        var existingGroup = await ctx.Repository.GetGroupByName(ctx.System.Id, groupName);
        if (existingGroup != null)
        {
            var msg =
                $"{Emojis.Warn} You already have a group in your system with the name \"{existingGroup.Name}\" (with ID `{existingGroup.DisplayHid(ctx.Config)}`). Do you want to create another group with the same name?";
            if (!await ctx.PromptYesNo(msg, "Create"))
                throw new PKError("Group creation cancelled.");
        }

        // todo: this is supposed to be a transaction, but it's not used in any useful way
        // consider removing it?
        using var conn = await ctx.Database.Obtain();
        var newGroup = await ctx.Repository.CreateGroup(ctx.System.Id, groupName);

        var dispatchData = new JObject();
        dispatchData.Add("name", groupName);

        if (ctx.Config.GroupDefaultPrivate)
        {
            var patch = new GroupPatch().WithAllPrivacy(PrivacyLevel.Private);
            await ctx.Repository.UpdateGroup(newGroup.Id, patch, conn);
            dispatchData.Merge(patch.ToJson());
        }

        _ = _dispatch.Dispatch(newGroup.Id, new UpdateDispatchData
        {
            Event = DispatchEvent.CREATE_GROUP,
            EventData = dispatchData
        });

        var reference = newGroup.Reference(ctx);

        var eb = new EmbedBuilder()
            .Description(
                $"Your new group, **{groupName}**, has been created, with the group ID **`{newGroup.DisplayHid(ctx.Config)}`**.\nBelow are a couple of useful commands:")
            .Field(new Embed.Field("View the group card", $"> {ctx.DefaultPrefix}group **{reference}**"))
            .Field(new Embed.Field("Add members to the group",
                $"> {ctx.DefaultPrefix}group **{reference}** add **MemberName**\n> {ctx.DefaultPrefix}group **{reference}** add **Member1** **Member2** **Member3** (and so on...)"))
            .Field(new Embed.Field("Set the description",
                $"> {ctx.DefaultPrefix}group **{reference}** description **This is my new group, and here is the description!**"))
            .Field(new Embed.Field("Set the group icon",
                $"> {ctx.DefaultPrefix}group **{reference}** icon\n*(with an image attached)*"));
        var replyStr = $"{Emojis.Success} Group created!";

        if (existingGroupCount >= Limits.WarnThreshold(groupLimit))
            replyStr += $"\n{Emojis.Warn} You are approaching the per-system group limit ({existingGroupCount} / {groupLimit} groups). Once you reach this limit, you will be unable to create new groups until existing groups are deleted, or you can ask for your limit to be raised in the PluralKit support server: <https://discord.gg/PczBt78>";

        await ctx.Reply(replyStr, eb.Build());
    }

    public async Task RenameGroup(Context ctx, PKGroup target)
    {
        ctx.CheckOwnGroup(target);

        // Check group name length
        var newName = ctx.RemainderOrNull() ?? throw new PKSyntaxError("You must pass a new group name.");
        if (newName.Length > Limits.MaxGroupNameLength)
            throw new PKError(
                $"New group name too long ({newName.Length}/{Limits.MaxMemberNameLength} characters).");

        // Warn if there's already a group by this name
        var existingGroup = await ctx.Repository.GetGroupByName(ctx.System.Id, newName);
        if (existingGroup != null && existingGroup.Id != target.Id)
        {
            var msg =
                $"{Emojis.Warn} You already have a group in your system with the name \"{existingGroup.Name}\" (with ID `{existingGroup.DisplayHid(ctx.Config)}`). Do you want to rename this group to that name too?";
            if (!await ctx.PromptYesNo(msg, "Rename"))
                throw new PKError("Group rename cancelled.");
        }

        await ctx.Repository.UpdateGroup(target.Id, new GroupPatch { Name = newName });

        await ctx.Reply($"{Emojis.Success} Group name changed from **{target.Name}** to **{newName}** (using {newName.Length}/{Limits.MaxGroupNameLength} characters).");
    }

    public async Task GroupDisplayName(Context ctx, PKGroup target)
    {
        var noDisplayNameSetMessage = "This group does not have a display name set" +
            (ctx.System?.Id == target.System
                ? $". To set one, type `{ctx.DefaultPrefix}group {target.Reference(ctx)} displayname <display name>`."
                : " or name is private.");

        // Whether displayname is shown or not should depend on if group name privacy is set.
        // If name privacy is on then displayname should look like name.

        var format = ctx.MatchFormat();

        // if we're doing a raw or plaintext query check for null
        if (format != ReplyFormat.Standard)
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
            var eb = new EmbedBuilder()
                .Description($"Showing displayname for group {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
            await ctx.Reply(target.DisplayName, embed: eb.Build());
            return;
        }

        if (!ctx.HasNext(false))
        {
            var showDisplayName = target.NamePrivacy.CanAccess(ctx.LookupContextFor(target.System)) && target.DisplayName != null;

            var eb = new EmbedBuilder()
                .Title("Group names")
                .Field(new Embed.Field("Name", target.NameFor(ctx)))
                .Field(new Embed.Field("Display Name", showDisplayName ? target.DisplayName : "*(no displayname set or name is private)*"));

            var reference = target.Reference(ctx);

            if (ctx.System?.Id == target.System)
                eb.Description(
                    $"To change display name, type `{ctx.DefaultPrefix}group {reference} displayname <display name>`.\n"
                    + $"To clear it, type `{ctx.DefaultPrefix}group {reference} displayname -clear`.\n"
                    + $"To print the raw display name, type `{ctx.DefaultPrefix}group {reference} displayname -raw`.");

            if (ctx.System?.Id == target.System && showDisplayName)
                eb.Footer(new Embed.EmbedFooter($"Using {target.DisplayName.Length}/{Limits.MaxGroupNameLength} characters."));

            await ctx.Reply(embed: eb.Build());

            return;
        }

        ctx.CheckOwnGroup(target);

        if (ctx.MatchClear() && await ctx.ConfirmClear("this group's display name"))
        {
            var patch = new GroupPatch { DisplayName = Partial<string>.Null() };
            await ctx.Repository.UpdateGroup(target.Id, patch);

            var replyStr = $"{Emojis.Success} Group display name cleared.";
            if (target.NamePrivacy == PrivacyLevel.Private)
                replyStr += $"\n{Emojis.Warn} Since this group no longer has a display name set, their name privacy **can no longer take effect**.";
            await ctx.Reply(replyStr);
        }
        else
        {
            var newDisplayName = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();
            if (newDisplayName.Length > Limits.MaxGroupNameLength)
                throw new PKError($"Group name too long ({newDisplayName.Length}/{Limits.MaxGroupNameLength} characters).");

            var patch = new GroupPatch { DisplayName = Partial<string>.Present(newDisplayName) };
            await ctx.Repository.UpdateGroup(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Group display name changed (using {newDisplayName.Length}/{Limits.MaxGroupNameLength} characters).");
        }
    }

    public async Task GroupDescription(Context ctx, PKGroup target)
    {
        ctx.CheckSystemPrivacy(target.System, target.DescriptionPrivacy);

        var noDescriptionSetMessage = "This group does not have a description set.";
        if (ctx.System?.Id == target.System)
            noDescriptionSetMessage +=
                $" To set one, type `{ctx.DefaultPrefix}group {target.Reference(ctx)} description <description>`.";

        var format = ctx.MatchFormat();

        // if there's nothing next or what's next is "raw"/"plaintext" we're doing a query, so check for null
        if (!ctx.HasNext(false) || format != ReplyFormat.Standard)
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
                .Description($"Showing description for group {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
            await ctx.Reply(target.Description, embed: eb.Build());
            return;
        }

        if (!ctx.HasNext(false))
        {
            await ctx.Reply(embed: new EmbedBuilder()
                .Title("Group description")
                .Description(target.Description)
                .Field(new Embed.Field("\u200B",
                    $"To print the description with formatting, type `{ctx.DefaultPrefix}group {target.Reference(ctx)} description -raw`."
                    + (ctx.System?.Id == target.System
                        ? $" To clear it, type `{ctx.DefaultPrefix}group {target.Reference(ctx)} description -clear`."
                        : "")
                        + $" Using {target.Description.Length}/{Limits.MaxDescriptionLength} characters."))
                .Build());
            return;
        }

        ctx.CheckOwnGroup(target);

        if (ctx.MatchClear() && await ctx.ConfirmClear("this group's description"))
        {
            var patch = new GroupPatch { Description = Partial<string>.Null() };
            await ctx.Repository.UpdateGroup(target.Id, patch);
            await ctx.Reply($"{Emojis.Success} Group description cleared.");
        }
        else
        {
            var description = ctx.RemainderOrNull(false).NormalizeLineEndSpacing();
            if (description.IsLongerThan(Limits.MaxDescriptionLength))
                throw Errors.StringTooLongError("Description", description.Length, Limits.MaxDescriptionLength);

            var patch = new GroupPatch { Description = Partial<string>.Present(description) };
            await ctx.Repository.UpdateGroup(target.Id, patch);

            await ctx.Reply($"{Emojis.Success} Group description changed (using {description.Length}/{Limits.MaxDescriptionLength} characters).");
        }
    }

    public async Task GroupIcon(Context ctx, PKGroup target)
    {
        async Task ClearIcon()
        {
            await ctx.ConfirmClear("this group's icon");
            ctx.CheckOwnGroup(target);

            await ctx.Repository.UpdateGroup(target.Id, new GroupPatch { Icon = null });
            await ctx.Reply($"{Emojis.Success} Group icon cleared.");
        }

        async Task SetIcon(ParsedImage img)
        {
            ctx.CheckOwnGroup(target);

            img = await _avatarHosting.TryRehostImage(img, AvatarHostingService.RehostedImageType.Avatar, ctx.Author.Id, ctx.System);
            await _avatarHosting.VerifyAvatarOrThrow(img.Url);

            await ctx.Repository.UpdateGroup(target.Id, new GroupPatch { Icon = img.CleanUrl ?? img.Url });

            var msg = img.Source switch
            {
                AvatarSource.User =>
                    $"{Emojis.Success} Group icon changed to {img.SourceUser?.Username}'s avatar!\n{Emojis.Warn} If {img.SourceUser?.Username} changes their avatar, the group icon will need to be re-set.",
                AvatarSource.Url => $"{Emojis.Success} Group icon changed to the image at the given URL.",
                AvatarSource.HostedCdn => $"{Emojis.Success} Group icon changed to attached image.",
                AvatarSource.Attachment =>
                    $"{Emojis.Success} Group icon changed to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the group icon will stop working.",
                _ => throw new ArgumentOutOfRangeException()
            };

            // The attachment's already right there, no need to preview it.
            var hasEmbed = img.Source != AvatarSource.Attachment && img.Source != AvatarSource.HostedCdn;
            await (hasEmbed
                ? ctx.Reply(msg, new EmbedBuilder().Image(new Embed.EmbedImage(img.Url)).Build())
                : ctx.Reply(msg));
        }

        async Task ShowIcon()
        {
            ctx.CheckSystemPrivacy(target.System, target.IconPrivacy);

            if ((target.Icon?.Trim() ?? "").Length > 0)
                switch (ctx.MatchFormat())
                {
                    case ReplyFormat.Raw:
                        await ctx.Reply($"`{target.Icon.TryGetCleanCdnUrl()}`");
                        break;
                    case ReplyFormat.Plaintext:
                        var ebP = new EmbedBuilder()
                            .Description($"Showing avatar for group {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
                        await ctx.Reply(text: $"<{target.Icon.TryGetCleanCdnUrl()}>", embed: ebP.Build());
                        break;
                    default:
                        var ebS = new EmbedBuilder()
                            .Title("Group icon")
                            .Image(new Embed.EmbedImage(target.Icon.TryGetCleanCdnUrl()));
                        if (target.System == ctx.System?.Id)
                            ebS.Description($"To clear, use `{ctx.DefaultPrefix}group {target.Reference(ctx)} icon -clear`.");
                        await ctx.Reply(embed: ebS.Build());
                        break;
                }
            else
                throw new PKSyntaxError(
                    "This group does not have an avatar set. Set one by attaching an image to this command, or by passing an image URL or @mention.");
        }

        if (ctx.MatchClear())
            await ClearIcon();
        else if (await ctx.MatchImage() is { } img)
            await SetIcon(img);
        else
            await ShowIcon();
    }

    public async Task GroupBannerImage(Context ctx, PKGroup target)
    {
        async Task ClearBannerImage()
        {
            ctx.CheckOwnGroup(target);
            await ctx.ConfirmClear("this group's banner image");

            await ctx.Repository.UpdateGroup(target.Id, new GroupPatch { BannerImage = null });
            await ctx.Reply($"{Emojis.Success} Group banner image cleared.");
        }

        async Task SetBannerImage(ParsedImage img)
        {
            ctx.CheckOwnGroup(target);

            img = await _avatarHosting.TryRehostImage(img, AvatarHostingService.RehostedImageType.Banner, ctx.Author.Id, ctx.System);
            await _avatarHosting.VerifyAvatarOrThrow(img.Url, true);

            await ctx.Repository.UpdateGroup(target.Id, new GroupPatch { BannerImage = img.CleanUrl ?? img.Url });

            var msg = img.Source switch
            {
                AvatarSource.Url => $"{Emojis.Success} Group banner image changed to the image at the given URL.",
                AvatarSource.HostedCdn => $"{Emojis.Success} Group banner image changed to attached image.",
                AvatarSource.Attachment =>
                    $"{Emojis.Success} Group banner image changed to attached image.\n{Emojis.Warn} If you delete the message containing the attachment, the banner image will stop working.",
                AvatarSource.User => throw new PKError("Cannot set a banner image to an user's avatar."),
                _ => throw new ArgumentOutOfRangeException()
            };

            // The attachment's already right there, no need to preview it.
            var hasEmbed = img.Source != AvatarSource.Attachment && img.Source != AvatarSource.HostedCdn;
            await (hasEmbed
                ? ctx.Reply(msg, new EmbedBuilder().Image(new Embed.EmbedImage(img.Url)).Build())
                : ctx.Reply(msg));
        }

        async Task ShowBannerImage()
        {
            ctx.CheckSystemPrivacy(target.System, target.BannerPrivacy);

            if ((target.BannerImage?.Trim() ?? "").Length > 0)
                switch (ctx.MatchFormat())
                {
                    case ReplyFormat.Raw:
                        await ctx.Reply($"`{target.BannerImage.TryGetCleanCdnUrl()}`");
                        break;
                    case ReplyFormat.Plaintext:
                        var ebP = new EmbedBuilder()
                            .Description($"Showing banner for group {target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
                        await ctx.Reply(text: $"<{target.BannerImage.TryGetCleanCdnUrl()}>", embed: ebP.Build());
                        break;
                    default:
                        var ebS = new EmbedBuilder()
                            .Title("Group banner image")
                            .Image(new Embed.EmbedImage(target.BannerImage.TryGetCleanCdnUrl()));
                        if (target.System == ctx.System?.Id)
                            ebS.Description($"To clear, use `{ctx.DefaultPrefix}group {target.Reference(ctx)} banner clear`.");
                        await ctx.Reply(embed: ebS.Build());
                        break;
                }
            else
                throw new PKSyntaxError(
                    "This group does not have a banner image set. Set one by attaching an image to this command, or by passing an image URL or @mention.");
        }

        if (ctx.MatchClear())
            await ClearBannerImage();
        else if (await ctx.MatchImage() is { } img)
            await SetBannerImage(img);
        else
            await ShowBannerImage();
    }

    public async Task GroupColor(Context ctx, PKGroup target)
    {
        var isOwnSystem = ctx.System?.Id == target.System;
        var matchedFormat = ctx.MatchFormat();
        var matchedClear = ctx.MatchClear();

        if (!isOwnSystem || !(ctx.HasNext() || matchedClear))
        {
            if (target.Color == null)
                await ctx.Reply(
                    "This group does not have a color set." + (isOwnSystem ? $" To set one, type `{ctx.DefaultPrefix}group {target.Reference(ctx)} color <color>`." : ""));
            else if (matchedFormat == ReplyFormat.Raw)
                await ctx.Reply("```\n#" + target.Color + "\n```");
            else if (matchedFormat == ReplyFormat.Plaintext)
                await ctx.Reply(target.Color);
            else
                await ctx.Reply(embed: new EmbedBuilder()
                    .Title("Group color")
                    .Color(target.Color.ToDiscordColor())
                    .Thumbnail(new Embed.EmbedThumbnail($"attachment://color.gif"))
                    .Description($"This group's color is **#{target.Color}**."
                        + (isOwnSystem ? $" To clear it, type `{ctx.DefaultPrefix}group {target.Reference(ctx)} color -clear`." : ""))
                    .Build(),
                    files: [MiscUtils.GenerateColorPreview(target.Color)]);
            return;
        }

        ctx.CheckSystem().CheckOwnGroup(target);

        if (matchedClear)
        {
            await ctx.Repository.UpdateGroup(target.Id, new() { Color = Partial<string>.Null() });

            await ctx.Reply($"{Emojis.Success} Group color cleared.");
        }
        else
        {
            var color = ctx.RemainderOrNull();

            if (color.StartsWith("#")) color = color.Substring(1);
            if (!Regex.IsMatch(color, "^[0-9a-fA-F]{6}$")) throw Errors.InvalidColorError(color);

            var patch = new GroupPatch { Color = Partial<string>.Present(color.ToLowerInvariant()) };
            await ctx.Repository.UpdateGroup(target.Id, patch);

            await ctx.Reply(embed: new EmbedBuilder()
                .Title($"{Emojis.Success} Group color changed.")
                .Color(color.ToDiscordColor())
                .Thumbnail(new Embed.EmbedThumbnail($"attachment://color.gif"))
                .Build(),
                files: [MiscUtils.GenerateColorPreview(color)]);
        }
    }

    public async Task ListSystemGroups(Context ctx, PKSystem system)
    {
        if (system == null)
        {
            ctx.CheckSystem();
            system = ctx.System;
        }

        ctx.CheckSystemPrivacy(system.Id, system.GroupListPrivacy);

        // explanation of privacy lookup here:
        // - ParseListOptions checks list access privacy and sets the privacy filter (which members show up in list)
        // - RenderGroupList checks the indivual privacy for each member (NameFor, etc)
        // the own system is always allowed to look up their list
        var opts = ctx.ParseListOptions(ctx.DirectLookupContextFor(system.Id), ctx.LookupContextFor(system.Id));
        await ctx.RenderGroupList(
            ctx.LookupContextFor(system.Id),
            system.Id,
            GetEmbedTitle(ctx, system, opts),
            system.Color,
            opts
        );
    }

    private string GetEmbedTitle(Context ctx, PKSystem target, ListOptions opts)
    {
        var title = new StringBuilder("Groups of ");

        if (target.NameFor(ctx) != null)
            title.Append($"{target.NameFor(ctx)} (`{target.DisplayHid(ctx.Config)}`)");
        else
            title.Append($"`{target.DisplayHid(ctx.Config)}`");

        if (opts.Search != null)
            title.Append($" matching **{opts.Search}**");

        return title.ToString();
    }

    public async Task ShowGroupCard(Context ctx, PKGroup target)
    {
        var system = await GetGroupSystem(ctx, target);
        await ctx.Reply(embed: await _embeds.CreateGroupEmbed(ctx, system, target));
    }

    public async Task GroupPrivacy(Context ctx, PKGroup target, PrivacyLevel? newValueFromCommand)
    {
        ctx.CheckSystem().CheckOwnGroup(target);
        // Display privacy settings
        if (!ctx.HasNext() && newValueFromCommand == null)
        {
            await ctx.Reply(embed: new EmbedBuilder()
                .Title($"Current privacy settings for {target.Name}")
                .Field(new Embed.Field("Name", target.NamePrivacy.Explanation()))
                .Field(new Embed.Field("Description", target.DescriptionPrivacy.Explanation()))
                .Field(new Embed.Field("Banner", target.BannerPrivacy.Explanation()))
                .Field(new Embed.Field("Icon", target.IconPrivacy.Explanation()))
                .Field(new Embed.Field("Member list", target.ListPrivacy.Explanation()))
                .Field(new Embed.Field("Metadata (creation date)", target.MetadataPrivacy.Explanation()))
                .Field(new Embed.Field("Visibility", target.Visibility.Explanation()))
                .Description(
                    $"To edit privacy settings, use the command:\n> {ctx.DefaultPrefix}group **{target.Reference(ctx)}** privacy **<subject>** **<level>**\n\n- `subject` is one of `name`, `description`, `banner`, `icon`, `members`, `metadata`, `visibility`, or `all`\n- `level` is either `public` or `private`.")
                .Build());
            return;
        }

        async Task SetAll(PrivacyLevel level)
        {
            await ctx.Repository.UpdateGroup(target.Id, new GroupPatch().WithAllPrivacy(level));

            if (level == PrivacyLevel.Private)
                await ctx.Reply(
                    $"{Emojis.Success} All {target.Name}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see nothing on the group card.");
            else
                await ctx.Reply(
                    $"{Emojis.Success} All {target.Name}'s privacy settings have been set to **{level.LevelName()}**. Other accounts will now see everything on the group card.");
        }

        async Task SetLevel(GroupPrivacySubject subject, PrivacyLevel level)
        {
            await ctx.Repository.UpdateGroup(target.Id, new GroupPatch().WithPrivacy(subject, level));

            var subjectName = subject switch
            {
                GroupPrivacySubject.Name => "name privacy",
                GroupPrivacySubject.Description => "description privacy",
                GroupPrivacySubject.Banner => "banner privacy",
                GroupPrivacySubject.Icon => "icon privacy",
                GroupPrivacySubject.List => "member list",
                GroupPrivacySubject.Metadata => "metadata",
                GroupPrivacySubject.Visibility => "visibility",
                _ => throw new ArgumentOutOfRangeException($"Unknown privacy subject {subject}")
            };

            var explanation = (subject, level) switch
            {
                (GroupPrivacySubject.Name, PrivacyLevel.Private) =>
                    "This group's name is now hidden from other systems, and will be replaced by the group's display name.",
                (GroupPrivacySubject.Description, PrivacyLevel.Private) =>
                    "This group's description is now hidden from other systems.",
                (GroupPrivacySubject.Banner, PrivacyLevel.Private) =>
                    "This group's banner is now hidden from other systems.",
                (GroupPrivacySubject.Icon, PrivacyLevel.Private) =>
                    "This group's icon is now hidden from other systems.",
                (GroupPrivacySubject.Visibility, PrivacyLevel.Private) =>
                    "This group is now hidden from group lists and member cards.",
                (GroupPrivacySubject.Metadata, PrivacyLevel.Private) =>
                    "This group's metadata (eg. creation date) is now hidden from other systems.",
                (GroupPrivacySubject.List, PrivacyLevel.Private) =>
                    "This group's member list is now hidden from other systems.",

                (GroupPrivacySubject.Name, PrivacyLevel.Public) =>
                    "This group's name is no longer hidden from other systems.",
                (GroupPrivacySubject.Description, PrivacyLevel.Public) =>
                    "This group's description is no longer hidden from other systems.",
                (GroupPrivacySubject.Banner, PrivacyLevel.Public) =>
                    "This group's banner is no longer hidden from other systems.",
                (GroupPrivacySubject.Icon, PrivacyLevel.Public) =>
                    "This group's icon is no longer hidden from other systems.",
                (GroupPrivacySubject.Visibility, PrivacyLevel.Public) =>
                    "This group is no longer hidden from group lists and member cards.",
                (GroupPrivacySubject.Metadata, PrivacyLevel.Public) =>
                    "This group's metadata (eg. creation date) is no longer hidden from other systems.",
                (GroupPrivacySubject.List, PrivacyLevel.Public) =>
                    "This group's member list is no longer hidden from other systems.",

                _ => throw new InvalidOperationException($"Invalid subject/level tuple ({subject}, {level})")
            };

            var replyStr = $"{Emojis.Success} {target.Name}'s **{subjectName}** has been set to **{level.LevelName()}**. {explanation}";

            if (subject == GroupPrivacySubject.Name && level == PrivacyLevel.Private && target.DisplayName == null)
                replyStr += $"\n{Emojis.Warn} This group does not have a display name set, and name privacy **will not take effect**.";

            await ctx.Reply(replyStr);
        }

        if (ctx.Match("all") || newValueFromCommand != null)
            await SetAll(newValueFromCommand ?? ctx.PopPrivacyLevel());
        else
            await SetLevel(ctx.PopGroupPrivacySubject(), ctx.PopPrivacyLevel());
    }

    public async Task DeleteGroup(Context ctx, PKGroup target)
    {
        ctx.CheckOwnGroup(target);

        await ctx.Reply(
            $"{Emojis.Warn} Are you sure you want to delete this group? If so, reply to this message with the group's ID (`{target.DisplayHid(ctx.Config)}`).\n**Note: this action is permanent.**");
        if (!await ctx.ConfirmWithReply(target.Hid, treatAsHid: true))
            throw new PKError(
                $"Group deletion cancelled. Note that you must reply with your group ID (`{target.DisplayHid(ctx.Config)}`) *verbatim*.");

        await ctx.Repository.DeleteGroup(target.Id);

        await ctx.Reply($"{Emojis.Success} Group deleted.");
    }

    public async Task DisplayId(Context ctx, PKGroup target)
    {
        await ctx.Reply(target.DisplayHid(ctx.Config));
    }

    private async Task<PKSystem> GetGroupSystem(Context ctx, PKGroup target)
    {
        var system = ctx.System;
        if (system?.Id == target.System)
            return system;
        return await ctx.Repository.GetSystem(target.System)!;
    }
}