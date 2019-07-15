using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using Discord.Commands;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Text;
using NodaTime.TimeZones;
using PluralKit.Core;

namespace PluralKit.Bot.Commands
{
    [Group("system")]
    [Alias("s")]
    public class SystemCommands : ContextParameterModuleBase<PKSystem>
    {
        public override string Prefix => "system";
        public override string ContextNoun => "system";

        public SystemStore Systems {get; set;}
        public MemberStore Members {get; set;}
        
        public SwitchStore Switches {get; set;}
        public EmbedService EmbedService {get; set;}
        

        [Command]
        [Remarks("system <name>")]
        public async Task Query(PKSystem system = null) {
            if (system == null) system = Context.SenderSystem;
            if (system == null) throw Errors.NoSystemError;

            await Context.Channel.SendMessageAsync(embed: await EmbedService.CreateSystemEmbed(system));
        }

        [Command("new")]
        [Alias("register", "create", "init", "add", "make")]
        [Remarks("system new <name>")]
        public async Task New([Remainder] string systemName = null)
        {
            if (ContextEntity != null) throw Errors.NotOwnSystemError;
            if (Context.SenderSystem != null) throw Errors.ExistingSystemError;

            var system = await Systems.Create(systemName);
            await Systems.Link(system, Context.User.Id);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} Your system has been created. Type `pk;system` to view it, and type `pk;help` for more information about commands you can use now.");
        }

        [Command("name")]
        [Alias("rename", "changename")]
        [Remarks("system name <name>")]
        [MustHaveSystem]
        public async Task Name([Remainder] string newSystemName = null) {
            if (newSystemName != null && newSystemName.Length > Limits.MaxSystemNameLength) throw Errors.SystemNameTooLongError(newSystemName.Length);

            Context.SenderSystem.Name = newSystemName;
            await Systems.Save(Context.SenderSystem);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} System name {(newSystemName != null ? "changed" : "cleared")}.");
        }

        [Command("description")]
        [Alias("desc")]
        [Remarks("system description <description>")]
        [MustHaveSystem]
        public async Task Description([Remainder] string newDescription = null) {
            if (newDescription != null && newDescription.Length > Limits.MaxDescriptionLength) throw Errors.DescriptionTooLongError(newDescription.Length);

            Context.SenderSystem.Description = newDescription;
            await Systems.Save(Context.SenderSystem);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} System description {(newDescription != null ? "changed" : "cleared")}.");
        }

        [Command("tag")]
        [Remarks("system tag <tag>")]
        [MustHaveSystem]
        public async Task Tag([Remainder] string newTag = null) {

            Context.SenderSystem.Tag = newTag;

            if (newTag != null)
            {
                if (newTag.Length > Limits.MaxSystemTagLength) throw Errors.SystemNameTooLongError(newTag.Length);

                // Check unproxyable messages *after* changing the tag (so it's seen in the method) but *before* we save to DB (so we can cancel)
                var unproxyableMembers = await Members.GetUnproxyableMembers(Context.SenderSystem);
                if (unproxyableMembers.Count > 0)
                {
                    var msg = await Context.Channel.SendMessageAsync(
                        $"{Emojis.Warn} Changing your system tag to '{newTag}' will result in the following members being unproxyable, since the tag would bring their name over 32 characters:\n**{string.Join(", ", unproxyableMembers.Select((m) => m.Name))}**\nDo you want to continue anyway?");
                    if (!await Context.PromptYesNo(msg)) throw new PKError("Tag change cancelled.");
                }
            }

            await Systems.Save(Context.SenderSystem);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} System tag {(newTag != null ? "changed" : "cleared")}.");
        }

        [Command("delete")]
        [Alias("remove", "destroy", "erase", "yeet")]
        [Remarks("system delete")]
        [MustHaveSystem]
        public async Task Delete() {
            var msg = await Context.Channel.SendMessageAsync($"{Emojis.Warn} Are you sure you want to delete your system? If so, reply to this message with your system's ID (`{Context.SenderSystem.Hid}`).\n**Note: this action is permanent.**");
            var reply = await Context.AwaitMessage(Context.Channel, Context.User, timeout: TimeSpan.FromMinutes(1));
            if (reply.Content != Context.SenderSystem.Hid) throw new PKError($"System deletion cancelled. Note that you must reply with your system ID (`{Context.SenderSystem.Hid}`) *verbatim*.");

            await Systems.Delete(Context.SenderSystem);
            await Context.Channel.SendMessageAsync($"{Emojis.Success} System deleted.");
        }

        [Group("list")]
        [Alias("l", "members")]
        public class SystemListCommands: ModuleBase<PKCommandContext> {
            public MemberStore Members { get; set; }

            [Command]
            [Remarks("system [system] list")]
            public async Task MemberShortList() {
                var system = Context.GetContextEntity<PKSystem>() ?? Context.SenderSystem;
                if (system == null) throw Errors.NoSystemError;

                var members = await Members.GetBySystem(system);
                var embedTitle = system.Name != null ? $"Members of {system.Name} (`{system.Hid}`)" : $"Members of `{system.Hid}`";
                await Context.Paginate<PKMember>(
                    members.OrderBy(m => m.Name).ToList(),
                    25,
                    embedTitle,
                    (eb, ms) => eb.Description = string.Join("\n", ms.Select((m) => {
                        if (m.HasProxyTags) return $"[`{m.Hid}`] **{m.Name}** *({m.ProxyString})*";
                        return $"[`{m.Hid}`] **{m.Name}**";
                    }))
                );
            }

            [Command("full")]
            [Alias("big", "details", "long")]
            [Remarks("system [system] list full")]
            public async Task MemberLongList() {
                var system = Context.GetContextEntity<PKSystem>() ?? Context.SenderSystem;
                if (system == null) throw Errors.NoSystemError;

                var members = await Members.GetBySystem(system);
                var embedTitle = system.Name != null ? $"Members of {system.Name} (`{system.Hid}`)" : $"Members of `{system.Hid}`";
                await Context.Paginate<PKMember>(
                    members.OrderBy(m => m.Name).ToList(),
                    10,
                    embedTitle,
                    (eb, ms) => {
                        foreach (var m in ms) {
                            var profile = $"**ID**: {m.Hid}";
                            if (m.Pronouns != null) profile += $"\n**Pronouns**: {m.Pronouns}";
                            if (m.Birthday != null) profile += $"\n**Birthdate**: {m.BirthdayString}";
                            if (m.Prefix != null || m.Suffix != null) profile += $"\n**Proxy tags**: {m.ProxyString}";
                            if (m.Description != null) profile += $"\n\n{m.Description}";
                            eb.AddField(m.Name, profile);
                        }
                    }
                );
            }
        }

        [Command("fronter")]
        [Alias("f", "front", "fronters")]
        [Remarks("system [system] fronter")]
        public async Task SystemFronter()
        {
            var system = ContextEntity ?? Context.SenderSystem;
            if (system == null) throw Errors.NoSystemError;
            
            var sw = await Switches.GetLatestSwitch(system);
            if (sw == null) throw Errors.NoRegisteredSwitches;
            
            await Context.Channel.SendMessageAsync(embed: await EmbedService.CreateFronterEmbed(sw, system.Zone));
        }

        [Command("fronthistory")]
        [Alias("fh", "history", "switches")]
        [Remarks("system [system] fronthistory")]
        public async Task SystemFrontHistory()
        {
            var system = ContextEntity ?? Context.SenderSystem;
            if (system == null) throw Errors.NoSystemError;

            var sws = (await Switches.GetSwitches(system, 10)).ToList();
            if (sws.Count == 0) throw Errors.NoRegisteredSwitches;
            
            await Context.Channel.SendMessageAsync(embed: await EmbedService.CreateFrontHistoryEmbed(sws, system.Zone));
        }

        [Command("frontpercent")]
        [Alias("frontbreakdown", "frontpercent", "front%", "fp")]
        [Remarks("system [system] frontpercent [duration]")]
        public async Task SystemFrontPercent([Remainder] string durationStr = "30d")
        {
            var system = ContextEntity ?? Context.SenderSystem;
            if (system == null) throw Errors.NoSystemError;

            var duration = PluralKit.Utils.ParsePeriod(durationStr);
            if (duration == null) throw Errors.InvalidDateTime(durationStr); 
            
            var rangeEnd = SystemClock.Instance.GetCurrentInstant();
            var rangeStart = rangeEnd - duration.Value;
            
            var frontpercent = await Switches.GetPerMemberSwitchDuration(system, rangeEnd - duration.Value, rangeEnd);
            await Context.Channel.SendMessageAsync(embed: await EmbedService.CreateFrontPercentEmbed(frontpercent, rangeStart.InZone(system.Zone)));
        }

        [Command("timezone")]
        [Alias("tz")]
        [Remarks("system timezone [timezone]")]
        [MustHaveSystem]
        public async Task SystemTimezone([Remainder] string zoneStr = null)
        {
            if (zoneStr == null)
            {
                Context.SenderSystem.UiTz = "UTC";
                await Systems.Save(Context.SenderSystem);
                await Context.Channel.SendMessageAsync($"{Emojis.Success} System time zone cleared.");
                return;
            }

            var zone = await FindTimeZone(zoneStr);
            if (zone == null) throw Errors.InvalidTimeZone(zoneStr);

            var currentTime = SystemClock.Instance.GetCurrentInstant().InZone(zone);
            var msg = await Context.Channel.SendMessageAsync(
                $"This will change the system time zone to {zone.Id}. The current time is {Formats.ZonedDateTimeFormat.Format(currentTime)}. Is this correct?");
            if (!await Context.PromptYesNo(msg)) throw Errors.TimezoneChangeCancelled;
            Context.SenderSystem.UiTz = zone.Id;
            await Systems.Save(Context.SenderSystem);

            await Context.Channel.SendMessageAsync($"System time zone changed to {zone.Id}.");
        }

        public async Task<DateTimeZone> FindTimeZone(string zoneStr) {
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
            return await Context.Choose("There were multiple matches for your time zone query. Please select the region that matches you the closest:", matchingZones,
                z =>
                {
                    if (TzdbDateTimeZoneSource.Default.Aliases.Contains(z.Id))
                        return $"**{z.Id}**, {string.Join(", ", TzdbDateTimeZoneSource.Default.Aliases[z.Id])}";

                    return $"**{z.Id}**";
                });
        } 

        public override async Task<PKSystem> ReadContextParameterAsync(string value)
        {
            var res = await new PKSystemTypeReader().ReadAsync(Context, value, _services);
            return res.IsSuccess ? res.BestMatch as PKSystem : null;
        }
    }
}