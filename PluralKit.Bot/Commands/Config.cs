using System.Text;

using Humanizer;
using Myriad.Builders;
using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;

using PluralKit.Core;

namespace PluralKit.Bot;
public class Config
{
    private record PaginatedConfigItem(string Key, string Description, string? CurrentValue, string DefaultValue);

    public async Task ShowConfig(Context ctx)
    {
        var items = new List<PaginatedConfigItem>();

        var allowAutoproxy = await ctx.Repository.GetAutoproxyEnabled(ctx.Author.Id);

        items.Add(new(
            "autoproxy account",
            "Whether autoproxy is enabled for the current account",
            EnabledDisabled(allowAutoproxy),
            "enabled"
        ));

        items.Add(new(
            "autoproxy timeout",
            "If this is set, latch-mode autoproxy will not keep autoproxying after this amount of time has elapsed since the last message sent in the server",
            ctx.Config.LatchTimeout.HasValue
                ? (
                    ctx.Config.LatchTimeout.Value != 0
                    ? Duration.FromSeconds(ctx.Config.LatchTimeout.Value).ToTimeSpan().Humanize(4)
                    : "disabled"
                )
                : null,
            ProxyMatcher.DefaultLatchExpiryTime.ToTimeSpan().Humanize(4)
        ));

        items.Add(new(
            "timezone",
            "The system's time zone - shows timestamps in your local time",
            ctx.Config.UiTz,
            "UTC"
        ));

        items.Add(new(
            "ping",
            $"Whether other users are able to mention you via a {Emojis.Bell} reaction",
            EnabledDisabled(ctx.Config.PingsEnabled),
            "enabled"
        ));

        items.Add(new(
            "private member",
            "Whether member privacy is automatically set to private for new members",
            EnabledDisabled(ctx.Config.MemberDefaultPrivate),
            "disabled"
        ));

        items.Add(new(
            "private group",
            "Whether group privacy is automatically set to private for new groups",
            EnabledDisabled(ctx.Config.GroupDefaultPrivate),
            "disabled"
        ));

        items.Add(new(
            "show private",
            "Whether private information is shown to linked accounts by default",
            ctx.Config.ShowPrivateInfo.ToString().ToLower(),
            "true"
        ));

        items.Add(new(
            "Member limit",
            "The maximum number of registered members for your system",
            ctx.Config.MemberLimitOverride?.ToString(),
            Limits.MaxMemberCount.ToString()
        ));

        items.Add(new(
            "Group limit",
            "The maximum number of registered groups for your system",
            ctx.Config.GroupLimitOverride?.ToString(),
            Limits.MaxGroupCount.ToString()
        ));

        items.Add(new(
            "Proxy case",
            "Whether proxy tags are case sensitive",
            EnabledDisabled(ctx.Config.CaseSensitiveProxyTags),
            "enabled"
        ));

        items.Add(new(
            "Proxy error",
            "Whether to send an error message when proxying fails",
            EnabledDisabled(ctx.Config.ProxyErrorMessageEnabled),
            "enabled"
        ));

        items.Add(new(
            "Split IDs",
            "Whether to display 6-character IDs split with a hyphen, to ease readability",
            EnabledDisabled(ctx.Config.HidDisplaySplit),
            "disabled"
        ));

        items.Add(new(
            "Capitalize IDs",
            "Whether to display IDs as capital letters, to ease readability",
            EnabledDisabled(ctx.Config.HidDisplayCaps),
            "disabled"
        ));

        items.Add(new(
            "Pad IDs",
            "Whether to pad 5-character IDs in lists (left/right)",
            ctx.Config.HidListPadding.ToUserString(),
            "off"
        ));

        items.Add(new(
            "show color",
            "Whether to show color codes in system/member/group cards",
            EnabledDisabled(ctx.Config.CardShowColorHex),
            "disabled"
        ));

        items.Add(new(
            "Proxy Switch",
            "Switching behavior when proxy tags are used",
            ctx.Config.ProxySwitch.ToUserString(),
            "off"
        ));

        items.Add(new(
            "Name Format",
            "Format string used to display a member's name https://pluralkit.me/guide/#setting-a-custom-name-format",
            ctx.Config.NameFormat,
            ProxyMember.DefaultFormat
        ));

        if (ctx.Guild == null)
        {
            items.Add(new(
                "Server Name Format",
                "Format string used to display a member's name in the current server",
                "only available in servers",
                "only available in servers"
            ));
        }
        else
        {
            items.Add(new(
                "Server Name Format",
                "Format string used to display a member's name in the current server",
                (await ctx.Repository.GetSystemGuild(ctx.Guild.Id, ctx.System.Id)).NameFormat ?? "none set",
                "none set"
            ));
        }

        await ctx.Paginate<PaginatedConfigItem>(
            items.ToAsyncEnumerable(),
            items.Count,
            10,
            "Current settings for your system",
            ctx.System.Color,
            (eb, l) =>
            {
                var description = new StringBuilder();

                foreach (var item in l)
                {
                    description.Append(item.Key.AsCode());
                    description.Append($" **({item.CurrentValue ?? item.DefaultValue})**");
                    if (item.CurrentValue != null && item.CurrentValue != item.DefaultValue)
                        description.Append("\ud83d\udd39");

                    description.AppendLine();
                    description.Append(item.Description);
                    description.AppendLine();
                    description.AppendLine();
                }

                eb.Description(description.ToString());

                // using *large* blue diamond here since it's easier to see in the small footer
                eb.Footer(new($"\U0001f537 means this setting was changed. Type `{ctx.DefaultPrefix}config <setting name> clear` to reset it to the default."));

                return Task.CompletedTask;
            }
        );
    }
    private string EnabledDisabled(bool value) => value ? "enabled" : "disabled";

    public async Task ViewAutoproxyAccount(Context ctx)
    {
        var allowAutoproxy = await ctx.Repository.GetAutoproxyEnabled(ctx.Author.Id);

        await ctx.Reply($"Autoproxy is currently **{EnabledDisabled(allowAutoproxy)}** for account <@{ctx.Author.Id}>.");
    }

    public async Task EditAutoproxyAccount(Context ctx, bool allow)
    {
        var allowAutoproxy = await ctx.Repository.GetAutoproxyEnabled(ctx.Author.Id);

        var statusString = EnabledDisabled(allow);
        if (allowAutoproxy == allow)
        {
            await ctx.Reply($"{Emojis.Note} Autoproxy is already {statusString} for account <@{ctx.Author.Id}>.");
            return;
        }
        var patch = new AccountPatch { AllowAutoproxy = allow };
        await ctx.Repository.UpdateAccount(ctx.Author.Id, patch);
        await ctx.Reply($"{Emojis.Success} Autoproxy {statusString} for account <@{ctx.Author.Id}>.");
    }

    public async Task ViewAutoproxyTimeout(Context ctx)
    {
        var timeout = ctx.Config.LatchTimeout.HasValue
        ? Duration.FromSeconds(ctx.Config.LatchTimeout.Value)
        : (Duration?)null;

        if (timeout == null)
            await ctx.Reply($"You do not have a custom autoproxy timeout duration set. The default latch timeout duration is {ProxyMatcher.DefaultLatchExpiryTime.ToTimeSpan().Humanize(4)}.");
        else if (timeout == Duration.Zero)
            await ctx.Reply("Latch timeout is currently **disabled** for your system. Latch mode autoproxy will never time out.");
        else
            await ctx.Reply($"The current latch timeout duration for your system is {timeout.Value.ToTimeSpan().Humanize(4)}.");
    }

    public async Task DisableAutoproxyTimeout(Context ctx)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { LatchTimeout = (int)Duration.Zero.TotalSeconds });

        await ctx.Reply($"{Emojis.Success} Latch timeout disabled. Latch mode autoproxy will never time out.");
    }

    public async Task ResetAutoproxyTimeout(Context ctx)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { LatchTimeout = null });

        await ctx.Reply($"{Emojis.Success} Latch timeout reset to default ({ProxyMatcher.DefaultLatchExpiryTime.ToTimeSpan().Humanize(4)}).");
    }

    public async Task EditAutoproxyTimeout(Context ctx, string timeout)
    {
        Duration newTimeout;
        Duration overflow = Duration.Zero;
        // todo: we should parse date in the command parser
        var timeoutStr = timeout;
        var timeoutPeriod = DateUtils.ParsePeriod(timeoutStr)
            ?? throw new PKError($"Could not parse '{timeoutStr}' as a valid duration. Try using a syntax such as \"3h5m\" (i.e. 3 hours and 5 minutes).");
        if (timeoutPeriod.TotalHours > 100000)
        {
            // sanity check to prevent seconds overflow if someone types in 999999999
            overflow = timeoutPeriod;
            newTimeout = Duration.Zero;
        }
        else newTimeout = timeoutPeriod;

        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { LatchTimeout = (int?)newTimeout.TotalSeconds });

        if (newTimeout == Duration.Zero && overflow != Duration.Zero)
            await ctx.Reply($"{Emojis.Success} Latch timeout disabled. Latch mode autoproxy will never time out. ({overflow.ToTimeSpan().Humanize(4)} is too long)");
        else if (newTimeout == Duration.Zero)
            await ctx.Reply($"{Emojis.Success} Latch timeout disabled. Latch mode autoproxy will never time out.");
        else
            await ctx.Reply($"{Emojis.Success} Latch timeout set to {newTimeout.ToTimeSpan().Humanize(4)}.");
    }

    public async Task ViewSystemTimezone(Context ctx)
    {
        if (ctx.System == null) throw Errors.NoSystemError(ctx.DefaultPrefix);

        await ctx.Reply(
            $"Your current system time zone is set to **{ctx.Config.UiTz}**. It is currently **{SystemClock.Instance.GetCurrentInstant().FormatZoned(ctx.Config.Zone)}** in that time zone. To change your system time zone, type `{ctx.DefaultPrefix}config tz <zone>`.");
    }

    public async Task ResetSystemTimezone(Context ctx)
    {
        if (ctx.System == null) throw Errors.NoSystemError(ctx.DefaultPrefix);

        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { UiTz = "UTC" });

        await ctx.Reply($"{Emojis.Success} System time zone cleared (set to UTC).");
    }

    public async Task EditSystemTimezone(Context ctx, string zoneStr, bool confirmYes = false)
    {
        if (ctx.System == null) throw Errors.NoSystemError(ctx.DefaultPrefix);

        var zone = await FindTimeZone(ctx, zoneStr);
        if (zone == null) throw Errors.InvalidTimeZone(zoneStr);

        var currentTime = SystemClock.Instance.GetCurrentInstant().InZone(zone);
        var msg = $"This will change the system time zone to **{zone.Id}**. The current time is **{currentTime.FormatZoned()}**. Is this correct?";
        if (!await ctx.PromptYesNo(msg, "Change Timezone", flagValue: confirmYes)) throw Errors.TimezoneChangeCancelled;

        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { UiTz = zone.Id });

        await ctx.Reply($"System time zone changed to **{zone.Id}**.");
    }


    private async Task<DateTimeZone> FindTimeZone(Context ctx, string zoneStr)
    {
        // First, if we're given a flag emoji, we extract the flag emoji code from it.
        zoneStr = Core.StringUtils.ExtractCountryFlag(zoneStr) ?? zoneStr;

        // Then, we find all *locations* matching either the given country code or the country name.
        var locations = TzdbDateTimeZoneSource.Default.Zone1970Locations;
        var matchingLocations = locations.Where(l => l.Countries.Any(c =>
            string.Equals(c.Code, zoneStr, StringComparison.InvariantCultureIgnoreCase) ||
            string.Equals(c.Name, zoneStr, StringComparison.InvariantCultureIgnoreCase)));

        // Then, we find all (unique) time zone IDs that match.
        var matchingZones = matchingLocations.Select(l => DateTimeZoneProviders.Tzdb.GetZoneOrNull(l.ZoneId))
            .Distinct().ToList();

        // If the set of matching zones is empty (ie. we didn't find anything), we try a few other things.
        if (matchingZones.Count == 0)
        {
            // First, we try to just find the time zone given directly and return that.
            var givenZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(zoneStr);
            if (givenZone != null) return givenZone;

            // If we didn't find anything there either, we try parsing the string as an offset, then
            // find all possible zones that match that offset. For an offset like UTC+2, this doesn't *quite*
            // work, since there are 57(!) matching zones (as of 2019-06-13) - but for less populated time zones
            // this could work nicely.
            var inputWithoutUtc = zoneStr.Replace("UTC", "").Replace("GMT", "");

            var res = OffsetPattern.CreateWithInvariantCulture("+H").Parse(inputWithoutUtc);
            if (!res.Success) res = OffsetPattern.CreateWithInvariantCulture("+H:mm").Parse(inputWithoutUtc);

            // If *this* didn't parse correctly, fuck it, bail.
            if (!res.Success) return null;
            var offset = res.Value;

            // To try to reduce the count, we go by locations from the 1970+ database instead of just the full database
            // This elides regions that have been identical since 1970, omitting small distinctions due to Ancient History(tm).
            var allZones = TzdbDateTimeZoneSource.Default.Zone1970Locations.Select(l => l.ZoneId).Distinct();
            matchingZones = allZones.Select(z => DateTimeZoneProviders.Tzdb.GetZoneOrNull(z))
                .Where(z => z.GetUtcOffset(SystemClock.Instance.GetCurrentInstant()) == offset).ToList();
        }

        // If we have a list of viable time zones, we ask the user which is correct.

        // If we only have one, return that one.
        if (matchingZones.Count == 1)
            return matchingZones.First();

        // Otherwise, prompt and return!
        return await ctx.Choose("There were multiple matches for your time zone query. Please select the region that matches you the closest:", matchingZones,
            z =>
            {
                if (TzdbDateTimeZoneSource.Default.Aliases.Contains(z.Id))
                    return $"**{z.Id}**, {string.Join(", ", TzdbDateTimeZoneSource.Default.Aliases[z.Id])}";

                return $"**{z.Id}**";
            });
    }

    public async Task ViewSystemPing(Context ctx)
    {
        // note: this is here because this is also used in `pk;system ping`, which does not CheckSystem
        ctx.CheckSystem();

        await ctx.Reply($"Reaction pings are currently **{EnabledDisabled(ctx.Config.PingsEnabled)}** for your system. " +
            $"To {EnabledDisabled(!ctx.Config.PingsEnabled)[..^1]} reaction pings, type `{ctx.DefaultPrefix}config ping {EnabledDisabled(!ctx.Config.PingsEnabled)[..^1]}`.");
    }

    public async Task EditSystemPing(Context ctx, bool value)
    {
        ctx.CheckSystem();

        if (ctx.Config.PingsEnabled == value)
        {
            await ctx.Reply($"Reaction pings are already **{EnabledDisabled(ctx.Config.PingsEnabled)}** for your system. " +
                $"To {EnabledDisabled(!value)[..^1]} reaction pings, type `{ctx.DefaultPrefix}config ping {EnabledDisabled(!value)[..^1]}`.");
        }
        else
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { PingsEnabled = value });
            await ctx.Reply($"Reaction pings have now been {EnabledDisabled(value)}.");
        }
    }

    public async Task ViewMemberDefaultPrivacy(Context ctx)
    {
        if (ctx.Config.MemberDefaultPrivate)
            await ctx.Reply($"Newly created members will currently have their privacy settings set to private. To change this, type `{ctx.DefaultPrefix}config private member off`");
        else
            await ctx.Reply($"Newly created members will currently have their privacy settings set to public. To automatically set new members' privacy settings to private, type `{ctx.DefaultPrefix}config private member on`");
    }

    public async Task EditMemberDefaultPrivacy(Context ctx, bool value)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { MemberDefaultPrivate = value });

        if (value)
            await ctx.Reply("Newly created members will now have their privacy settings set to private.");
        else
            await ctx.Reply("Newly created members will now have their privacy settings set to public.");
    }

    public async Task ViewGroupDefaultPrivacy(Context ctx)
    {
        if (ctx.Config.GroupDefaultPrivate)
            await ctx.Reply($"Newly created groups will currently have their privacy settings set to private. To change this, type `{ctx.DefaultPrefix}config private group off`");
        else
            await ctx.Reply($"Newly created groups will currently have their privacy settings set to public. To automatically set new groups' privacy settings to private, type `{ctx.DefaultPrefix}config private group on`");
    }

    public async Task EditGroupDefaultPrivacy(Context ctx, bool value)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { GroupDefaultPrivate = value });

        if (value)
            await ctx.Reply("Newly created groups will now have their privacy settings set to private.");
        else
            await ctx.Reply("Newly created groups will now have their privacy settings set to public.");
    }

    public async Task ViewShowPrivateInfo(Context ctx)
    {
        if (ctx.Config.ShowPrivateInfo)
            await ctx.Reply("Private information is currently **shown** when looking up your own info. Use the `-public` flag to hide it.");
        else
            await ctx.Reply("Private information is currently **hidden** when looking up your own info. Use the `-private` flag to show it.");
    }

    public async Task EditShowPrivateInfo(Context ctx, bool value)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { ShowPrivateInfo = value });

        if (value)
            await ctx.Reply("Private information will now be **shown** when looking up your own info. Use the `-public` flag to hide it.");
        else
            await ctx.Reply("Private information will now be **hidden** when looking up your own info. Use the `-private` flag to show it.");
    }

    public async Task ViewCaseSensitiveProxyTags(Context ctx)
    {
        if (ctx.Config.CaseSensitiveProxyTags)
            await ctx.Reply("Proxy tags are currently case **sensitive**.");
        else
            await ctx.Reply("Proxy tags are currently case **insensitive**.");
    }

    public async Task EditCaseSensitiveProxyTags(Context ctx, bool value)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { CaseSensitiveProxyTags = value });

        if (value)
            await ctx.Reply("Proxy tags are now case sensitive.");
        else
            await ctx.Reply("Proxy tags are now case insensitive.");
    }

    public async Task ViewProxyErrorMessageEnabled(Context ctx)
    {
        if (ctx.Config.ProxyErrorMessageEnabled)
            await ctx.Reply("Proxy error messages are currently **enabled**.");
        else
            await ctx.Reply("Proxy error messages are currently **disabled**. Messages that fail to proxy (due to message or attachment size) will not throw an error message.");
    }

    public async Task EditProxyErrorMessageEnabled(Context ctx, bool value)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { ProxyErrorMessageEnabled = value });

        if (value)
            await ctx.Reply("Proxy error messages are now enabled.");
        else
            await ctx.Reply("Proxy error messages are now disabled. Messages that fail to proxy (due to message or attachment size) will not throw an error message.");
    }

    public async Task ViewHidDisplaySplit(Context ctx)
    {
        await ctx.Reply($"Splitting of 6-character IDs with a hyphen is currently **{EnabledDisabled(ctx.Config.HidDisplaySplit)}**.");
    }

    public async Task EditHidDisplaySplit(Context ctx, bool value)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { HidDisplaySplit = value });
        await ctx.Reply($"Splitting of 6-character IDs with a hyphen is now {EnabledDisabled(value)}.");
    }

    public async Task ViewHidDisplayCaps(Context ctx)
    {
        await ctx.Reply($"Displaying IDs as capital letters is currently **{EnabledDisabled(ctx.Config.HidDisplayCaps)}**.");
    }

    public async Task EditHidDisplayCaps(Context ctx, bool value)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { HidDisplayCaps = value });
        await ctx.Reply($"Displaying IDs as capital letters is now {EnabledDisabled(value)}.");
    }

    public async Task ViewHidListPadding(Context ctx)
    {
        string message = ctx.Config.HidListPadding switch
        {
            SystemConfig.HidPadFormat.None => "Padding 5-character IDs in lists is currently disabled.",
            SystemConfig.HidPadFormat.Left => "5-character IDs displayed in lists will have a padding space added to the beginning.",
            SystemConfig.HidPadFormat.Right => "5-character IDs displayed in lists will have a padding space added to the end.",
            _ => throw new Exception("unreachable")
        };
        await ctx.Reply(message);
    }

    public async Task EditHidListPadding(Context ctx, string padding)
    {
        var badInputError = "Valid padding settings are `left`, `right`, or `off`.";

        if (padding.Equals("off", StringComparison.InvariantCultureIgnoreCase))
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { HidListPadding = SystemConfig.HidPadFormat.None });
            await ctx.Reply("Padding 5-character IDs in lists has been disabled.");
        }
        else if (padding.Equals("left", StringComparison.InvariantCultureIgnoreCase) || padding.Equals("l", StringComparison.InvariantCultureIgnoreCase))
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { HidListPadding = SystemConfig.HidPadFormat.Left });
            await ctx.Reply("5-character IDs displayed in lists will now have a padding space added to the beginning.");
        }
        else if (padding.Equals("right", StringComparison.InvariantCultureIgnoreCase) || padding.Equals("r", StringComparison.InvariantCultureIgnoreCase))
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { HidListPadding = SystemConfig.HidPadFormat.Right });
            await ctx.Reply("5-character IDs displayed in lists will now have a padding space added to the end.");
        }
        else
        {
            throw new PKError(badInputError);
        }
    }

    public async Task ViewCardShowColorHex(Context ctx)
    {
        await ctx.Reply($"Showing color codes on system/member/group cards is currently **{EnabledDisabled(ctx.Config.CardShowColorHex)}**.");
    }

    public async Task EditCardShowColorHex(Context ctx, bool value)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { CardShowColorHex = value });
        await ctx.Reply($"Showing color codes on system/member/group cards is now {EnabledDisabled(value)}.");
    }

    public async Task ViewProxySwitch(Context ctx)
    {
        string msg = ctx.Config.ProxySwitch switch
        {
            SystemConfig.ProxySwitchAction.Off => "Currently, when you proxy as a member, no switches are logged or changed.",
            SystemConfig.ProxySwitchAction.New => "When you proxy as a member, currently it makes a new switch.",
            SystemConfig.ProxySwitchAction.Add => "When you proxy as a member, currently it adds them to the current switch.",
            _ => throw new Exception("unreachable"),
        };
        await ctx.Reply(msg);
    }

    public async Task EditProxySwitch(Context ctx, SystemConfig.ProxySwitchAction action)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { ProxySwitch = action });
        switch (action)
        {
            case SystemConfig.ProxySwitchAction.Off: await ctx.Reply("Now when you proxy as a member, no switches are logged or changed."); break;
            case SystemConfig.ProxySwitchAction.New: await ctx.Reply("When you proxy as a member, it now makes a new switch."); break;
            case SystemConfig.ProxySwitchAction.Add: await ctx.Reply("When you proxy as a member, it now adds them to the current switch."); break;
            default: throw new Exception("unreachable");
        }
    }

    public async Task ViewNameFormat(Context ctx)
    {
        await ctx.Reply($"Member names are currently formatted as `{ctx.Config.NameFormat ?? ProxyMember.DefaultFormat}`");
    }

    public async Task ResetNameFormat(Context ctx)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { NameFormat = ProxyMember.DefaultFormat });
        await ctx.Reply($"Member names are now formatted as `{ProxyMember.DefaultFormat}`");
    }

    public async Task EditNameFormat(Context ctx, string formatString)
    {
        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { NameFormat = formatString });
        await ctx.Reply($"Member names are now formatted as `{formatString}`");
    }

    public async Task ViewServerNameFormat(Context ctx, ReplyFormat format)
    {
        ctx.CheckGuildContext();

        var guildCfg = await ctx.Repository.GetSystemGuild(ctx.Guild.Id, ctx.System.Id);
        if (guildCfg.NameFormat == null)
            await ctx.Reply("You do not have a specific name format set for this server and member names are formatted with your global name format.");
        else
            switch (format)
            {
                case ReplyFormat.Raw:
                    await ctx.Reply($"`{guildCfg.NameFormat}`");
                    break;
                case ReplyFormat.Plaintext:
                    var eb = new EmbedBuilder()
                        .Description($"Showing guild Name Format for system {ctx.System.DisplayHid(ctx.Config)}");
                    await ctx.Reply(guildCfg.NameFormat, eb.Build());
                    break;
                default:
                    await ctx.Reply($"Your member names in this server are currently formatted as `{guildCfg.NameFormat}`");
                    break;
            }
    }

    public async Task ResetServerNameFormat(Context ctx)
    {
        ctx.CheckGuildContext();

        await ctx.Repository.UpdateSystemGuild(ctx.System.Id, ctx.Guild.Id, new() { NameFormat = null });
        await ctx.Reply($"Member names are now formatted with your global name format in this server.");
    }

    public async Task EditServerNameFormat(Context ctx, string formatString)
    {
        ctx.CheckGuildContext();

        await ctx.Repository.UpdateSystemGuild(ctx.System.Id, ctx.Guild.Id, new() { NameFormat = formatString });
        await ctx.Reply($"Member names are now formatted as `{formatString}` in this server.");
    }

    public Task LimitUpdate(Context ctx)
    {
        throw new PKError("You cannot update your own member or group limits. If you need a limit update, please join the " +
        "support server and ask in #bot-support: https://discord.gg/PczBt78");
    }
}