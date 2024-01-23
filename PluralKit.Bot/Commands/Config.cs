using System.Text;

using Humanizer;

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
                eb.Footer(new("\U0001f537 means this setting was changed. Type `pk;config <setting name> clear` to reset it to the default."));

                return Task.CompletedTask;
            }
        );
    }
    private string EnabledDisabled(bool value) => value ? "enabled" : "disabled";

    public async Task AutoproxyAccount(Context ctx)
    {
        var allowAutoproxy = await ctx.Repository.GetAutoproxyEnabled(ctx.Author.Id);

        if (!ctx.HasNext())
        {
            await ctx.Reply($"Autoproxy is currently **{EnabledDisabled(allowAutoproxy)}** for account <@{ctx.Author.Id}>.");
            return;
        }

        var allow = ctx.MatchToggle(true);

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


    public async Task AutoproxyTimeout(Context ctx)
    {
        if (!ctx.HasNext())
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
            return;
        }

        Duration? newTimeout;
        Duration overflow = Duration.Zero;
        if (ctx.Match("off", "stop", "cancel", "no", "disable", "remove")) newTimeout = Duration.Zero;
        else if (ctx.MatchClear()) newTimeout = null;
        else
        {
            var timeoutStr = ctx.RemainderOrNull();
            var timeoutPeriod = DateUtils.ParsePeriod(timeoutStr);
            if (timeoutPeriod == null) throw new PKError($"Could not parse '{timeoutStr}' as a valid duration. Try using a syntax such as \"3h5m\" (i.e. 3 hours and 5 minutes).");
            if (timeoutPeriod.Value.TotalHours > 100000)
            {
                // sanity check to prevent seconds overflow if someone types in 999999999
                overflow = timeoutPeriod.Value;
                newTimeout = Duration.Zero;
            }
            else newTimeout = timeoutPeriod;
        }

        await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { LatchTimeout = (int?)newTimeout?.TotalSeconds });

        if (newTimeout == null)
            await ctx.Reply($"{Emojis.Success} Latch timeout reset to default ({ProxyMatcher.DefaultLatchExpiryTime.ToTimeSpan().Humanize(4)}).");
        else if (newTimeout == Duration.Zero && overflow != Duration.Zero)
            await ctx.Reply($"{Emojis.Success} Latch timeout disabled. Latch mode autoproxy will never time out. ({overflow.ToTimeSpan().Humanize(4)} is too long)");
        else if (newTimeout == Duration.Zero)
            await ctx.Reply($"{Emojis.Success} Latch timeout disabled. Latch mode autoproxy will never time out.");
        else
            await ctx.Reply($"{Emojis.Success} Latch timeout set to {newTimeout.Value!.ToTimeSpan().Humanize(4)}.");
    }

    public async Task SystemTimezone(Context ctx)
    {
        if (ctx.System == null) throw Errors.NoSystemError;

        if (ctx.MatchClear())
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { UiTz = "UTC" });

            await ctx.Reply($"{Emojis.Success} System time zone cleared (set to UTC).");
            return;
        }

        var zoneStr = ctx.RemainderOrNull();
        if (zoneStr == null)
        {
            await ctx.Reply(
                $"Your current system time zone is set to **{ctx.Config.UiTz}**. It is currently **{SystemClock.Instance.GetCurrentInstant().FormatZoned(ctx.Config.Zone)}** in that time zone. To change your system time zone, type `pk;config tz <zone>`.");
            return;
        }

        var zone = await FindTimeZone(ctx, zoneStr);
        if (zone == null) throw Errors.InvalidTimeZone(zoneStr);

        var currentTime = SystemClock.Instance.GetCurrentInstant().InZone(zone);
        var msg = $"This will change the system time zone to **{zone.Id}**. The current time is **{currentTime.FormatZoned()}**. Is this correct?";
        if (!await ctx.PromptYesNo(msg, "Change Timezone")) throw Errors.TimezoneChangeCancelled;

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

    public async Task SystemPing(Context ctx)
    {
        // note: this is here because this is also used in `pk;system ping`, which does not CheckSystem
        ctx.CheckSystem();

        // todo: move all the other config settings to this format

        String Response(bool isError, bool val)
            => $"Reaction pings are {(isError ? "already" : "currently")} **{EnabledDisabled(val)}** for your system. "
             + $"To {EnabledDisabled(!val)[..^1]} reaction pings, type `pk;config ping {EnabledDisabled(!val)[..^1]}`.";

        if (!ctx.HasNext())
        {
            await ctx.Reply(Response(false, ctx.Config.PingsEnabled));
            return;
        }

        var value = ctx.MatchToggle(true);

        if (ctx.Config.PingsEnabled == value)
            await ctx.Reply(Response(true, ctx.Config.PingsEnabled));
        else
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { PingsEnabled = value });
            await ctx.Reply($"Reaction pings have now been {EnabledDisabled(value)}.");
        }
    }

    public async Task MemberDefaultPrivacy(Context ctx)
    {
        if (!ctx.HasNext())
        {
            if (ctx.Config.MemberDefaultPrivate) { await ctx.Reply("Newly created members will currently have their privacy settings set to private. To change this, type `pk;config private member off`"); }
            else { await ctx.Reply("Newly created members will currently have their privacy settings set to public. To automatically set new members' privacy settings to private, type `pk;config private member on`"); }
        }
        else
        {
            if (ctx.MatchToggle(false))
            {
                await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { MemberDefaultPrivate = true });

                await ctx.Reply("Newly created members will now have their privacy settings set to private.");
            }
            else
            {
                await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { MemberDefaultPrivate = false });

                await ctx.Reply("Newly created members will now have their privacy settings set to public.");
            }
        }
    }

    public async Task GroupDefaultPrivacy(Context ctx)
    {
        if (!ctx.HasNext())
        {
            if (ctx.Config.GroupDefaultPrivate) { await ctx.Reply("Newly created groups will currently have their privacy settings set to private. To change this, type `pk;config private group off`"); }
            else { await ctx.Reply("Newly created groups will currently have their privacy settings set to public. To automatically set new groups' privacy settings to private, type `pk;config private group on`"); }
        }
        else
        {
            if (ctx.MatchToggle(false))
            {
                await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { GroupDefaultPrivate = true });

                await ctx.Reply("Newly created groups will now have their privacy settings set to private.");
            }
            else
            {
                await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { GroupDefaultPrivate = false });

                await ctx.Reply("Newly created groups will now have their privacy settings set to public.");
            }
        }
    }

    public async Task ShowPrivateInfo(Context ctx)
    {
        if (!ctx.HasNext())
        {
            if (ctx.Config.ShowPrivateInfo) await ctx.Reply("Private information is currently **shown** when looking up your own info. Use the `-public` flag to hide it.");
            else await ctx.Reply("Private information is currently **hidden** when looking up your own info. Use the `-private` flag to show it.");
            return;
        }

        if (ctx.MatchToggle(true))
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { ShowPrivateInfo = true });

            await ctx.Reply("Private information will now be **shown** when looking up your own info. Use the `-public` flag to hide it.");
        }
        else
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { ShowPrivateInfo = false });

            await ctx.Reply("Private information will now be **hidden** when looking up your own info. Use the `-private` flag to show it.");
        }
    }

    public async Task CaseSensitiveProxyTags(Context ctx)
    {
        if (!ctx.HasNext())
        {
            if (ctx.Config.CaseSensitiveProxyTags) { await ctx.Reply("Proxy tags are currently case **sensitive**."); }
            else { await ctx.Reply("Proxy tags are currently case **insensitive**."); }
            return;
        }

        if (ctx.MatchToggle(true))
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { CaseSensitiveProxyTags = true });

            await ctx.Reply("Proxy tags are now case sensitive.");
        }
        else
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { CaseSensitiveProxyTags = false });

            await ctx.Reply("Proxy tags are now case insensitive.");
        }
    }

    public async Task ProxyErrorMessageEnabled(Context ctx)
    {
        if (!ctx.HasNext())
        {
            if (ctx.Config.ProxyErrorMessageEnabled) { await ctx.Reply("Proxy error messages are currently **enabled**."); }
            else { await ctx.Reply("Proxy error messages are currently **disabled**. Messages that fail to proxy (due to message or attachment size) will not throw an error message."); }
            return;
        }

        if (ctx.MatchToggle(true))
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { ProxyErrorMessageEnabled = true });

            await ctx.Reply("Proxy error messages are now enabled.");
        }
        else
        {
            await ctx.Repository.UpdateSystemConfig(ctx.System.Id, new() { ProxyErrorMessageEnabled = false });

            await ctx.Reply("Proxy error messages are now disabled. Messages that fail to proxy (due to message or attachment size) will not throw an error message.");
        }
    }
}