using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Humanizer;
using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;

using PluralKit.Bot.CommandSystem;
using PluralKit.Core;

namespace PluralKit.Bot.Commands
{
    public class SystemCommands
    {
        private IDataStore _data;
        private EmbedService _embeds;

        private ProxyCacheService _proxyCache;

        public SystemCommands(EmbedService embeds, ProxyCacheService proxyCache, IDataStore data)
        {
            _embeds = embeds;
            _proxyCache = proxyCache;
            _data = data;
        }
        
        public async Task Query(Context ctx, PKSystem system) {
            if (system == null) throw Errors.NoSystemError;

            await ctx.Reply(embed: await _embeds.CreateSystemEmbed(system));
        }
        
        public async Task New(Context ctx)
        {
            ctx.CheckNoSystem();

            var system = await _data.CreateSystem(ctx.RemainderOrNull());
            await _data.AddAccount(system, ctx.Author.Id);
            await ctx.Reply($"{Emojis.Success} Your system has been created. Type `pk;system` to view it, and type `pk;help` for more information about commands you can use now.");
        }
        
        public async Task Name(Context ctx)
        {
            ctx.CheckSystem();

            var newSystemName = ctx.RemainderOrNull();
            if (newSystemName != null && newSystemName.Length > Limits.MaxSystemNameLength) throw Errors.SystemNameTooLongError(newSystemName.Length);

            ctx.System.Name = newSystemName;
            await _data.SaveSystem(ctx.System);
            await ctx.Reply($"{Emojis.Success} System name {(newSystemName != null ? "changed" : "cleared")}.");
        }
        
        public async Task Description(Context ctx) {
            ctx.CheckSystem();

            var newDescription = ctx.RemainderOrNull();
            if (newDescription != null && newDescription.Length > Limits.MaxDescriptionLength) throw Errors.DescriptionTooLongError(newDescription.Length);

            ctx.System.Description = newDescription;
            await _data.SaveSystem(ctx.System);
            await ctx.Reply($"{Emojis.Success} System description {(newDescription != null ? "changed" : "cleared")}.");
        }
        
        public async Task Tag(Context ctx)
        {
            ctx.CheckSystem();

            var newTag = ctx.RemainderOrNull();
            ctx.System.Tag = newTag;

            if (newTag != null)
                if (newTag.Length > Limits.MaxSystemTagLength)
                    throw Errors.SystemNameTooLongError(newTag.Length);

            await _data.SaveSystem(ctx.System);
            await ctx.Reply($"{Emojis.Success} System tag {(newTag != null ? $"changed. Member names will now end with `{newTag.SanitizeMentions()}` when proxied." : "cleared")}.");
            
            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }
        
        public async Task SystemAvatar(Context ctx)
        {
            ctx.CheckSystem();

            var member = await ctx.MatchUser();
            if (member != null)
            {
                if (member.AvatarId == null) throw Errors.UserHasNoAvatar;
                ctx.System.AvatarUrl = member.GetAvatarUrl(ImageFormat.Png, size: 256);
                await _data.SaveSystem(ctx.System);
            
                var embed = new EmbedBuilder().WithImageUrl(ctx.System.AvatarUrl).Build();
                await ctx.Reply(
                    $"{Emojis.Success} System avatar changed to {member.Username}'s avatar! {Emojis.Warn} Please note that if {member.Username} changes their avatar, the system's avatar will need to be re-set.", embed: embed);
            }
            else
            {
                string url = ctx.RemainderOrNull() ?? ctx.Message.Attachments.FirstOrDefault()?.ProxyUrl;
                if (url != null) await ctx.BusyIndicator(() => Utils.VerifyAvatarOrThrow(url));

                ctx.System.AvatarUrl = url;
                await _data.SaveSystem(ctx.System);

                var embed = url != null ? new EmbedBuilder().WithImageUrl(url).Build() : null;
                await ctx.Reply($"{Emojis.Success} System avatar {(url == null ? "cleared" : "changed")}.", embed: embed);
            }
            
            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }
        
        public async Task Delete(Context ctx) {
            ctx.CheckSystem();

            var msg = await ctx.Reply($"{Emojis.Warn} Are you sure you want to delete your system? If so, reply to this message with your system's ID (`{ctx.System.Hid}`).\n**Note: this action is permanent.**");
            var reply = await ctx.AwaitMessage(ctx.Channel, ctx.Author, timeout: TimeSpan.FromMinutes(1));
            if (reply.Content != ctx.System.Hid) throw new PKError($"System deletion cancelled. Note that you must reply with your system ID (`{ctx.System.Hid}`) *verbatim*.");

            await _data.DeleteSystem(ctx.System);
            await ctx.Reply($"{Emojis.Success} System deleted.");
            
            await _proxyCache.InvalidateResultsForSystem(ctx.System);
        }
        
        public async Task MemberShortList(Context ctx, PKSystem system) {
            if (system == null) throw Errors.NoSystemError;

            var members = await _data.GetSystemMembers(system);
            var embedTitle = system.Name != null ? $"Members of {system.Name.SanitizeMentions()} (`{system.Hid}`)" : $"Members of `{system.Hid}`";
            await ctx.Paginate<PKMember>(
                members.OrderBy(m => m.Name.ToLower()).ToList(),
                25,
                embedTitle,
                (eb, ms) => eb.Description = string.Join("\n", ms.Select((m) => {
                    if (m.HasProxyTags) return $"[`{m.Hid}`] **{m.Name.SanitizeMentions()}** *({m.ProxyTagsString().SanitizeMentions()})*";
                    return $"[`{m.Hid}`] **{m.Name.SanitizeMentions()}**";
                }))
            );
        }

        public async Task MemberLongList(Context ctx, PKSystem system) {
            if (system == null) throw Errors.NoSystemError;

            var members = await _data.GetSystemMembers(system);
            var embedTitle = system.Name != null ? $"Members of {system.Name} (`{system.Hid}`)" : $"Members of `{system.Hid}`";
            await ctx.Paginate<PKMember>(
                members.OrderBy(m => m.Name.ToLower()).ToList(),
                5,
                embedTitle,
                (eb, ms) => {
                    foreach (var m in ms) {
                        var profile = $"**ID**: {m.Hid}";
                        if (m.Pronouns != null) profile += $"\n**Pronouns**: {m.Pronouns}";
                        if (m.Birthday != null) profile += $"\n**Birthdate**: {m.BirthdayString}";
                        if (m.ProxyTags.Count > 0) profile += $"\n**Proxy tags:** {m.ProxyTagsString()}";
                        if (m.Description != null) profile += $"\n\n{m.Description}";
                        eb.AddField(m.Name, profile.Truncate(1024));
                    }
                }
            );
        }
        
        public async Task SystemFronter(Context ctx, PKSystem system)
        {
            if (system == null) throw Errors.NoSystemError;
            
            var sw = await _data.GetLatestSwitch(system);
            if (sw == null) throw Errors.NoRegisteredSwitches;
            
            await ctx.Reply(embed: await _embeds.CreateFronterEmbed(sw, system.Zone));
        }

        public async Task SystemFrontHistory(Context ctx, PKSystem system)
        {
            if (system == null) throw Errors.NoSystemError;

            // Determine page size
            string pageSizeStr = ctx.RemainderOrNull();
            int.TryParse(pageSizeStr, out int itemsPerPage);
            // null or negative page size inputs default to 10 items per page
            if (itemsPerPage <= 0)
                itemsPerPage = 10;
            // cap the items per page at 25 - this is an arbitrary value, but past this point the headroom for actual content is pretty small
            if (itemsPerPage > 25)
                itemsPerPage = 25;

            // Get all of this system's switches and their members
            var sws = (await _data.GetPeriodFronters(ctx.System, Instant.FromDateTimeUtc(DateTime.MinValue.ToUniversalTime()), SystemClock.Instance.GetCurrentInstant())).ToList();
            if (sws.Count == 0) throw Errors.NoRegisteredSwitches;
            var lastSwitchTime = sws.Max(x => x.TimespanStart); // Determine when the last switch occurred for conditional display of duration
            var embedTitle = system.Name != null
                ? $"Past switches of {system.Name} (`{system.Hid}`)"
                : $"Past switches of `{system.Hid}`";

            // Specify items per page and how long each line can be to keep within embed limits
            // e.g. If we're doing 10 items per page, give each line 1/10 of the max description length, minus one character for line breaks
            var embedLineMaxLength = (EmbedBuilder.MaxDescriptionLength / itemsPerPage) - 1;
            await ctx.Paginate<SwitchListEntry>(
                sws.OrderByDescending(m => m.TimespanStart).ToList(),
                itemsPerPage,
                embedTitle,
                (eb, fh) =>
                    eb.Description = string.Join("\n", fh.Select((sw) =>
                        {
                            // Display switch members, when the switch occurred, the time since the switch, and the switch duration (if known - the last switch is still active)
                            // e.g. Members (timestamp, time since switch occurred, switch duration)
                            var membersStr = sw.Members.Any() ? string.Join(", ", sw.Members.Select(m => m.Name)) : "no fronter";
                            var switchSince = SystemClock.Instance.GetCurrentInstant() - sw.TimespanStart;
                            var switchDuration = sw.TimespanEnd - sw.TimespanStart;

                            // Construct the time/duration portion
                            var switchInfo = sw.TimespanStart.Equals(lastSwitchTime)
                                ? $"({Formats.ZonedDateTimeFormat.Format(sw.TimespanStart.InZone(ctx.System.Zone))}, {Formats.DurationFormat.Format(switchSince)} ago)"
                                : $"({Formats.ZonedDateTimeFormat.Format(sw.TimespanStart.InZone(ctx.System.Zone))}, {Formats.DurationFormat.Format(switchSince)} ago, for {Formats.DurationFormat.Format(switchDuration)})";
                            // Construct the switch members portion, truncating if it's too long
                            var memberInfo = (membersStr.Length + switchInfo.Length + 5 > embedLineMaxLength) // Check the combined length of the member list, time/duration, and formatting characters
                                ? $"**{membersStr.Truncate(embedLineMaxLength - switchInfo.Length - 5)}** " // Resulting string is too long - truncate the member list to make it fit
                                : $"**{membersStr}** "; // Resulting string is short enough, use the full member list
                            return memberInfo + switchInfo;
                        }))
            );
        }

        public async Task SystemFrontPercent(Context ctx, PKSystem system)
        {
            if (system == null) throw Errors.NoSystemError;

            string durationStr = ctx.RemainderOrNull() ?? "30d";
            
            var now = SystemClock.Instance.GetCurrentInstant();

            var rangeStart = PluralKit.Utils.ParseDateTime(durationStr, true, system.Zone);
            if (rangeStart == null) throw Errors.InvalidDateTime(durationStr);
            if (rangeStart.Value.ToInstant() > now) throw Errors.FrontPercentTimeInFuture;
            
            var frontpercent = await _data.GetFrontBreakdown(system, rangeStart.Value.ToInstant(), now);
            await ctx.Reply(embed: await _embeds.CreateFrontPercentEmbed(frontpercent, system.Zone));
        }
        
        public async Task SystemTimezone(Context ctx)
        {
            if (ctx.System == null) throw Errors.NoSystemError;

            var zoneStr = ctx.RemainderOrNull();
            if (zoneStr == null)
            {
                ctx.System.UiTz = "UTC";
                await _data.SaveSystem(ctx.System);
                await ctx.Reply($"{Emojis.Success} System time zone cleared.");
                return;
            }

            var zone = await FindTimeZone(ctx, zoneStr);
            if (zone == null) throw Errors.InvalidTimeZone(zoneStr);

            var currentTime = SystemClock.Instance.GetCurrentInstant().InZone(zone);
            var msg = await ctx.Reply(
                $"This will change the system time zone to {zone.Id}. The current time is {Formats.ZonedDateTimeFormat.Format(currentTime)}. Is this correct?");
            if (!await ctx.PromptYesNo(msg)) throw Errors.TimezoneChangeCancelled;
            ctx.System.UiTz = zone.Id;
            await _data.SaveSystem(ctx.System);

            await ctx.Reply($"System time zone changed to {zone.Id}.");
        }

        public async Task<DateTimeZone> FindTimeZone(Context ctx, string zoneStr) {
            // First, if we're given a flag emoji, we extract the flag emoji code from it.
            zoneStr = PluralKit.Utils.ExtractCountryFlag(zoneStr) ?? zoneStr;
            
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
    }
}
