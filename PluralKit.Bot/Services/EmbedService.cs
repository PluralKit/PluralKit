using Humanizer;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot;

public class EmbedService
{
    private readonly IDiscordCache _cache;
    private readonly IDatabase _db;
    private readonly ModelRepository _repo;
    private readonly DiscordApiClient _rest;

    public EmbedService(IDatabase db, ModelRepository repo, IDiscordCache cache, DiscordApiClient rest)
    {
        _db = db;
        _repo = repo;
        _cache = cache;
        _rest = rest;
    }

    private Task<(ulong Id, User? User)[]> GetUsers(IEnumerable<ulong> ids)
    {
        async Task<(ulong Id, User? User)> Inner(ulong id)
        {
            var user = await _cache.GetOrFetchUser(_rest, id);
            return (id, user);
        }

        return Task.WhenAll(ids.Select(Inner));
    }

    public async Task<Embed> CreateSystemEmbed(Context cctx, PKSystem system, LookupContext ctx)
    {
        // Fetch/render info for all accounts simultaneously
        var accounts = await _repo.GetSystemAccounts(system.Id);
        var users = (await GetUsers(accounts)).Select(x => x.User?.NameAndMention() ?? $"(deleted account {x.Id})");

        var countctx = LookupContext.ByNonOwner;
        if (cctx.MatchFlag("a", "all"))
        {
            if (system.Id == cctx.System.Id)
                countctx = LookupContext.ByOwner;
            else
                throw Errors.LookupNotAllowed;
        }

        var memberCount = await _repo.GetSystemMemberCount(system.Id, countctx == LookupContext.ByOwner ? null : PrivacyLevel.Public);

        uint color;
        try
        {
            color = system.Color?.ToDiscordColor() ?? DiscordUtils.Gray;
        }
        catch (ArgumentException)
        {
            // There's no API for system colors yet, but defaulting to a blank color in advance can't be a bad idea
            color = DiscordUtils.Gray;
        }

        var eb = new EmbedBuilder()
            .Title(system.NameFor(ctx))
            .Footer(new Embed.EmbedFooter(
                $"System ID: {system.Hid} | Created on {system.Created.FormatZoned(cctx.Zone)}"))
            .Color(color)
            .Url($"https://dash.pluralkit.me/profile/s/{system.Hid}");

        var avatar = system.AvatarFor(ctx);
        if (avatar != null)
            eb.Thumbnail(new Embed.EmbedThumbnail(avatar));

        if (system.DescriptionPrivacy.CanAccess(ctx))
            eb.Image(new Embed.EmbedImage(system.BannerImage));

        var latestSwitch = await _repo.GetLatestSwitch(system.Id);
        if (latestSwitch != null && system.FrontPrivacy.CanAccess(ctx))
        {
            var switchMembers =
                await _db.Execute(conn => _repo.GetSwitchMembers(conn, latestSwitch.Id)).ToListAsync();
            if (switchMembers.Count > 0)
            {
                var memberStr = string.Join(", ", switchMembers.Select(m => m.NameFor(ctx)));
                if (memberStr.Length > 200)
                    memberStr = $"[too many to show, see `pk;system {system.Hid} fronters`]";
                eb.Field(new Embed.Field("Fronter".ToQuantity(switchMembers.Count, ShowQuantityAs.None), memberStr));
            }
        }

        if (system.Tag != null)
            eb.Field(new Embed.Field("Tag", system.Tag.EscapeMarkdown(), true));

        if (cctx.Guild != null)
        {
            var guildSettings = await _repo.GetSystemGuild(cctx.Guild.Id, system.Id);

            if (guildSettings.Tag != null && guildSettings.TagEnabled)
                eb.Field(new Embed.Field($"Tag (in server '{cctx.Guild.Name}')", guildSettings.Tag
                    .EscapeMarkdown(), true));

            if (!guildSettings.TagEnabled)
                eb.Field(new Embed.Field($"Tag (in server '{cctx.Guild.Name}')",
                    "*(tag is disabled in this server)*"));

            if (guildSettings.DisplayName != null)
                eb.Title(guildSettings.DisplayName);

            var guildAvatar = guildSettings.AvatarUrl.TryGetCleanCdnUrl();
            if (guildAvatar != null)
            {
                eb.Thumbnail(new Embed.EmbedThumbnail(guildAvatar));
                var sysDesc = "*(this system has a server-specific avatar set";
                if (avatar != null)
                    sysDesc += $"; [click here]({system.AvatarUrl.TryGetCleanCdnUrl()}) to see their global avatar)*";
                else
                    sysDesc += ")*";
                eb.Description(sysDesc);
            }
        }

        if (system.PronounPrivacy.CanAccess(ctx) && system.Pronouns != null)
            eb.Field(new Embed.Field("Pronouns", system.Pronouns, true));

        if (!system.Color.EmptyOrNull()) eb.Field(new Embed.Field("Color", $"#{system.Color}", true));

        eb.Field(new Embed.Field("Linked accounts", string.Join("\n", users).Truncate(1000), true));

        if (system.MemberListPrivacy.CanAccess(ctx))
        {
            if (memberCount > 0)
                eb.Field(new Embed.Field($"Members ({memberCount})",
                    $"(see `pk;system {system.Hid} list` or `pk;system {system.Hid} list full`)", true));
            else
                eb.Field(new Embed.Field($"Members ({memberCount})", "Add one with `pk;member new`!", true));
        }

        if (system.DescriptionFor(ctx) is { } desc)
            eb.Field(new Embed.Field("Description", desc.NormalizeLineEndSpacing().Truncate(1024)));

        return eb.Build();
    }

    public Embed CreateLoggedMessageEmbed(Message triggerMessage, Message proxiedMessage, string systemHid,
                                          PKMember member, string channelName, string oldContent = null)
    {
        // TODO: pronouns in ?-reacted response using this card
        var timestamp = DiscordUtils.SnowflakeToInstant(proxiedMessage.Id);
        var name = proxiedMessage.Author.Username;
        // sometimes Discord will just... not return the avatar hash with webhook messages
        var avatar = proxiedMessage.Author.Avatar != null
            ? proxiedMessage.Author.AvatarUrl()
            : member.WebhookAvatarFor(LookupContext.ByNonOwner);
        var embed = new EmbedBuilder()
            .Author(new Embed.EmbedAuthor($"#{channelName}: {name}", IconUrl: avatar))
            .Thumbnail(new Embed.EmbedThumbnail(avatar))
            .Description(proxiedMessage.Content?.NormalizeLineEndSpacing())
            .Footer(new Embed.EmbedFooter(
                $"System ID: {systemHid} | Member ID: {member.Hid} | Sender: {triggerMessage.Author.Username}#{triggerMessage.Author.Discriminator} ({triggerMessage.Author.Id}) | Message ID: {proxiedMessage.Id} | Original Message ID: {triggerMessage.Id}"))
            .Timestamp(timestamp.ToDateTimeOffset().ToString("O"));

        if (oldContent == "")
            oldContent = "*no message content*";

        if (oldContent != null)
            embed.Field(new Embed.Field("Old message", oldContent?.NormalizeLineEndSpacing().Truncate(1000)));

        return embed.Build();
    }

    public async Task<Embed> CreateMemberEmbed(PKSystem system, PKMember member, Guild guild, LookupContext ctx, DateTimeZone zone)
    {
        // string FormatTimestamp(Instant timestamp) => DateTimeFormats.ZonedDateTimeFormat.Format(timestamp.InZone(system.Zone));

        var name = member.NameFor(ctx);
        var systemGuildSettings = guild != null ? await _repo.GetSystemGuild(guild.Id, system.Id) : null;
        if (systemGuildSettings != null && systemGuildSettings.DisplayName != null)
            name = $"{name} ({systemGuildSettings.DisplayName})";
        else if (system.NameFor(ctx) != null)
            name = $"{name} ({system.NameFor(ctx)})";
        else
            name = $"{name}";

        uint color;
        try
        {
            color = member.Color?.ToDiscordColor() ?? DiscordUtils.Gray;
        }
        catch (ArgumentException)
        {
            // Bad API use can cause an invalid color string
            // this is now fixed in the API, but might still have some remnants in the database
            // so we just default to a blank color, yolo
            color = DiscordUtils.Gray;
        }

        var guildSettings = guild != null ? await _repo.GetMemberGuild(guild.Id, member.Id) : null;
        var guildDisplayName = guildSettings?.DisplayName;
        var webhook_avatar = guildSettings?.AvatarUrl ?? member.WebhookAvatarFor(ctx) ?? member.AvatarFor(ctx);
        var avatar = guildSettings?.AvatarUrl ?? member.AvatarFor(ctx);

        var groups = await _repo.GetMemberGroups(member.Id)
            .Where(g => g.Visibility.CanAccess(ctx))
            .OrderBy(g => g.Name, StringComparer.InvariantCultureIgnoreCase)
            .ToListAsync();

        var eb = new EmbedBuilder()
            .Author(new Embed.EmbedAuthor(name, IconUrl: webhook_avatar.TryGetCleanCdnUrl(), Url: $"https://dash.pluralkit.me/profile/m/{member.Hid}"))
            // .WithColor(member.ColorPrivacy.CanAccess(ctx) ? color : DiscordUtils.Gray)
            .Color(color)
            .Footer(new Embed.EmbedFooter(
                $"System ID: {system.Hid} | Member ID: {member.Hid} {(member.MetadataPrivacy.CanAccess(ctx) ? $"| Created on {member.Created.FormatZoned(zone)}" : "")}"));

        if (member.DescriptionPrivacy.CanAccess(ctx))
            eb.Image(new Embed.EmbedImage(member.BannerImage));

        var description = "";
        if (member.MemberVisibility == PrivacyLevel.Private) description += "*(this member is hidden)*\n";
        if (guildSettings?.AvatarUrl != null)
            if (member.AvatarFor(ctx) != null)
                description +=
                    $"*(this member has a server-specific avatar set; [click here]({member.AvatarUrl.TryGetCleanCdnUrl()}) to see the global avatar)*\n";
            else
                description += "*(this member has a server-specific avatar set)*\n";
        if (description != "") eb.Description(description);

        if (avatar != null) eb.Thumbnail(new Embed.EmbedThumbnail(avatar.TryGetCleanCdnUrl()));

        if (!member.DisplayName.EmptyOrNull() && member.NamePrivacy.CanAccess(ctx))
            eb.Field(new Embed.Field("Display Name", member.DisplayName.Truncate(1024), true));
        if (guild != null && guildDisplayName != null)
            eb.Field(new Embed.Field($"Server Nickname (for {guild.Name})", guildDisplayName.Truncate(1024), true));
        if (member.BirthdayFor(ctx) != null) eb.Field(new Embed.Field("Birthdate", member.BirthdayString, true));
        if (member.PronounsFor(ctx) is { } pronouns && !string.IsNullOrWhiteSpace(pronouns))
            eb.Field(new Embed.Field("Pronouns", pronouns.Truncate(1024), true));
        if (member.MessageCountFor(ctx) is { } count && count > 0)
            eb.Field(new Embed.Field("Message Count", member.MessageCount.ToString(), true));
        if (member.HasProxyTags && member.ProxyPrivacy.CanAccess(ctx))
            eb.Field(new Embed.Field("Proxy Tags", member.ProxyTagsString("\n").Truncate(1024), true));
        // --- For when this gets added to the member object itself or however they get added
        // if (member.LastMessage != null && member.MetadataPrivacy.CanAccess(ctx)) eb.AddField("Last message:" FormatTimestamp(DiscordUtils.SnowflakeToInstant(m.LastMessage.Value)));
        // if (member.LastSwitchTime != null && m.MetadataPrivacy.CanAccess(ctx)) eb.AddField("Last switched in:", FormatTimestamp(member.LastSwitchTime.Value));
        // if (!member.Color.EmptyOrNull() && member.ColorPrivacy.CanAccess(ctx)) eb.AddField("Color", $"#{member.Color}", true);
        if (!member.Color.EmptyOrNull()) eb.Field(new Embed.Field("Color", $"#{member.Color}", true));

        if (groups.Count > 0)
        {
            // More than 5 groups show in "compact" format without ID
            var content = groups.Count > 5
                ? string.Join(", ", groups.Select(g => g.DisplayName ?? g.Name))
                : string.Join("\n", groups.Select(g => $"[`{g.Hid}`] **{g.DisplayName ?? g.Name}**"));
            eb.Field(new Embed.Field($"Groups ({groups.Count})", content.Truncate(1000)));
        }

        if (member.DescriptionFor(ctx) is { } desc)
            eb.Field(new Embed.Field("Description", member.Description.NormalizeLineEndSpacing()));

        return eb.Build();
    }

    public async Task<Embed> CreateGroupEmbed(Context ctx, PKSystem system, PKGroup target)
    {
        var pctx = ctx.LookupContextFor(system.Id);

        var countctx = LookupContext.ByNonOwner;
        if (ctx.MatchFlag("a", "all"))
        {
            if (system.Id == ctx.System.Id)
                countctx = LookupContext.ByOwner;
            else
                throw Errors.LookupNotAllowed;
        }

        var memberCount = await _repo.GetGroupMemberCount(target.Id, countctx == LookupContext.ByOwner ? null : PrivacyLevel.Public);

        var nameField = target.NameFor(ctx);
        var systemGuildSettings = ctx.Guild != null ? await _repo.GetSystemGuild(ctx.Guild.Id, system.Id) : null;
        if (systemGuildSettings != null && systemGuildSettings.DisplayName != null)
            nameField = $"{nameField} ({systemGuildSettings.DisplayName})";
        else if (system.NameFor(ctx) != null)
            nameField = $"{nameField} ({system.NameFor(ctx)})";
        else
            nameField = $"{nameField} ({system.Name})";

        uint color;
        try
        {
            color = target.Color?.ToDiscordColor() ?? DiscordUtils.Gray;
        }
        catch (ArgumentException)
        {
            // There's no API for group colors yet, but defaulting to a blank color regardless
            color = DiscordUtils.Gray;
        }

        var eb = new EmbedBuilder()
            .Author(new Embed.EmbedAuthor(nameField, IconUrl: target.IconFor(pctx), Url: $"https://dash.pluralkit.me/profile/g/{target.Hid}"))
            .Color(color);

        eb.Footer(new Embed.EmbedFooter($"System ID: {system.Hid} | Group ID: {target.Hid}{(target.MetadataPrivacy.CanAccess(pctx) ? $" | Created on {target.Created.FormatZoned(ctx.Zone)}" : "")}"));

        if (target.DescriptionPrivacy.CanAccess(pctx))
            eb.Image(new Embed.EmbedImage(target.BannerImage));

        if (target.NamePrivacy.CanAccess(pctx) && target.DisplayName != null)
            eb.Field(new Embed.Field("Display Name", target.DisplayName, true));

        if (!target.Color.EmptyOrNull()) eb.Field(new Embed.Field("Color", $"#{target.Color}", true));

        if (target.ListPrivacy.CanAccess(pctx))
        {
            if (memberCount == 0 && pctx == LookupContext.ByOwner)
                // Only suggest the add command if this is actually the owner lol
                eb.Field(new Embed.Field("Members (0)",
                    $"Add one with `pk;group {target.Reference(ctx)} add <member>`!"));
            else
            {
                var name = pctx == LookupContext.ByOwner
                    ? target.Reference(ctx)
                    : target.Hid;
                eb.Field(new Embed.Field($"Members ({memberCount})", $"(see `pk;group {name} list`)"));
            }
        }

        if (target.DescriptionFor(pctx) is { } desc)
            eb.Field(new Embed.Field("Description", desc));

        if (target.IconFor(pctx) is { } icon)
            eb.Thumbnail(new Embed.EmbedThumbnail(icon.TryGetCleanCdnUrl()));

        return eb.Build();
    }

    public async Task<Embed> CreateFronterEmbed(PKSwitch sw, DateTimeZone zone, LookupContext ctx)
    {
        var members = await _db.Execute(c => _repo.GetSwitchMembers(c, sw.Id).ToListAsync().AsTask());
        var timeSinceSwitch = SystemClock.Instance.GetCurrentInstant() - sw.Timestamp;
        var memberStr = "*(no fronter)*";
        if (members.Count > 0)
        {
            memberStr = "";
            foreach (var item in members.Select((value, i) => new { i, value }))
            {
                memberStr += item.i == 0 ? "" : ", ";
                // field limit is 1024, capping after 900 gives us plenty of room
                // for the remaining count message
                if (memberStr.Length < 900)
                    memberStr += item.value.NameFor(ctx);
                else
                {
                    memberStr += $"*({members.Count - item.i} not shown)*";
                    break;
                }
            }
        }

        return new EmbedBuilder()
            .Color(members.FirstOrDefault()?.Color?.ToDiscordColor() ?? DiscordUtils.Gray)
            .Field(new Embed.Field($"Current {"fronter".ToQuantity(members.Count, ShowQuantityAs.None)}", memberStr))
            .Field(new Embed.Field("Since",
                $"{sw.Timestamp.FormatZoned(zone)} ({timeSinceSwitch.FormatDuration()} ago)"))
            .Build();
    }

    public async Task<Embed> CreateMessageInfoEmbed(FullMessage msg, bool showContent)
    {
        var channel = await _cache.GetOrFetchChannel(_rest, msg.Message.Channel);
        var ctx = LookupContext.ByNonOwner;

        var serverMsg = await _rest.GetMessageOrNull(msg.Message.Channel, msg.Message.Mid);

        // Need this whole dance to handle cases where:
        // - the user is deleted (userInfo == null)
        // - the bot's no longer in the server we're querying (channel == null)
        // - the member is no longer in the server we're querying (memberInfo == null)
        // TODO: optimize ordering here a bit with new cache impl; and figure what happens if bot leaves server -> channel still cached -> hits this bit and 401s?
        GuildMemberPartial memberInfo = null;
        User userInfo = null;
        if (channel != null)
        {
            GuildMember member = null;
            try
            {
                member = await _rest.GetGuildMember(channel.GuildId!.Value, msg.Message.Sender);
            }
            catch (ForbiddenException)
            {
                // no permission, couldn't fetch, oh well
            }

            if (member != null)
                // Don't do an extra request if we already have this info from the member lookup
                userInfo = member.User;
            memberInfo = member;
        }

        if (userInfo == null)
            userInfo = await _cache.GetOrFetchUser(_rest, msg.Message.Sender);

        // Calculate string displayed under "Sent by"
        string userStr;
        if (showContent && memberInfo != null && memberInfo.Nick != null)
            userStr = $"**Username:** {userInfo.NameAndMention()}\n**Nickname:** {memberInfo.Nick}";
        else if (userInfo != null) userStr = userInfo.NameAndMention();
        else userStr = $"*(deleted user {msg.Message.Sender})*";

        var content = serverMsg?.Content?.NormalizeLineEndSpacing();
        if (content == null || !showContent)
            content = "*(message contents deleted or inaccessible)*";

        // Put it all together
        var eb = new EmbedBuilder()
            .Author(new Embed.EmbedAuthor(msg.Member?.NameFor(ctx) ?? "(deleted member)",
                IconUrl: msg.Member?.AvatarFor(ctx).TryGetCleanCdnUrl()))
            .Description(content)
            .Image(showContent ? new Embed.EmbedImage(serverMsg?.Attachments?.FirstOrDefault()?.Url) : null)
            .Field(new Embed.Field("System",
                msg.System == null
                    ? "*(deleted or unknown system)*"
                    : msg.System.NameFor(ctx) != null ? $"{msg.System.NameFor(ctx)} (`{msg.System.Hid}`)" : $"`{msg.System.Hid}`"
            , true))
            .Field(new Embed.Field("Member",
                msg.Member == null
                    ? "*(deleted member)*"
                    : $"{msg.Member.NameFor(ctx)} (`{msg.Member.Hid}`)"
            , true))
            .Field(new Embed.Field("Sent by", userStr, true))
            .Timestamp(DiscordUtils.SnowflakeToInstant(msg.Message.Mid).ToDateTimeOffset().ToString("O"))
            .Footer(new Embed.EmbedFooter($"Original Message ID: {msg.Message.OriginalMid}"));

        var roles = memberInfo?.Roles?.ToList();
        if (roles != null && roles.Count > 0 && showContent)
        {
            var rolesString = string.Join(", ", (await Task.WhenAll(roles
                    .Select(async id =>
                    {
                        var role = await _cache.TryGetRole(id);
                        if (role != null)
                            return role;
                        return new Role { Name = "*(unknown role)*", Position = 0 };
                    })))
                .OrderByDescending(role => role.Position)
                .Select(role => role.Name));
            eb.Field(new Embed.Field($"Account roles ({roles.Count})", rolesString.Truncate(1024)));
        }

        return eb.Build();
    }

    public Task<Embed> CreateFrontPercentEmbed(FrontBreakdown breakdown, PKSystem system, PKGroup group,
                                               DateTimeZone tz, LookupContext ctx, string embedTitle,
                                               bool ignoreNoFronters, bool showFlat)
    {
        var color = system.Color;
        if (group != null) color = group.Color;

        uint embedColor;
        try
        {
            embedColor = color?.ToDiscordColor() ?? DiscordUtils.Gray;
        }
        catch (ArgumentException)
        {
            embedColor = DiscordUtils.Gray;
        }

        var eb = new EmbedBuilder()
            .Title(embedTitle)
            .Color(embedColor);

        var footer =
            $"Since {breakdown.RangeStart.FormatZoned(tz)} ({(breakdown.RangeEnd - breakdown.RangeStart).FormatDuration()} ago)";

        Duration period;

        if (showFlat)
        {
            period = Duration.FromTicks(breakdown.MemberSwitchDurations.Values.ToList().Sum(i => i.TotalTicks));
            footer += ". Showing flat list (percentages add up to 100%)";
            if (!ignoreNoFronters) period += breakdown.NoFronterDuration;
            else footer += ", ignoring switch-outs";
        }
        else if (ignoreNoFronters)
        {
            period = breakdown.RangeEnd - breakdown.RangeStart - breakdown.NoFronterDuration;
            footer += ". Ignoring switch-outs";
        }
        else
        {
            period = breakdown.RangeEnd - breakdown.RangeStart;
        }

        eb.Footer(new Embed.EmbedFooter(footer));

        var maxEntriesToDisplay = 24; // max 25 fields allowed in embed - reserve 1 for "others"

        // We convert to a list of pairs so we can add the no-fronter value
        // Dictionary doesn't allow for null keys so we instead have a pair with a null key ;)
        var pairs = breakdown.MemberSwitchDurations.ToList();
        if (breakdown.NoFronterDuration != Duration.Zero && !ignoreNoFronters)
            pairs.Add(new KeyValuePair<PKMember, Duration>(null, breakdown.NoFronterDuration));

        var membersOrdered = pairs.OrderByDescending(pair => pair.Value).Take(maxEntriesToDisplay).ToList();
        foreach (var pair in membersOrdered)
        {
            var frac = pair.Value / period;
            eb.Field(new Embed.Field(pair.Key?.NameFor(ctx) ?? "*(no fronter)*",
                $"{frac * 100:F0}% ({pair.Value.FormatDuration()})"));
        }

        if (membersOrdered.Count > maxEntriesToDisplay)
            eb.Field(new Embed.Field("(others)",
                membersOrdered.Skip(maxEntriesToDisplay)
                    .Aggregate(Duration.Zero, (prod, next) => prod + next.Value)
                    .FormatDuration(), true));

        return Task.FromResult(eb.Build());
    }
}