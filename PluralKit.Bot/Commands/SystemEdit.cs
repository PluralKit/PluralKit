using System;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using DSharpPlus;
using DSharpPlus.Entities;

using NodaTime;
using NodaTime.Text;
using NodaTime.TimeZones;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class SystemEdit
    {
        private IDataStore _data;
        private IDatabase _db;
        private EmbedService _embeds;

        public SystemEdit(IDataStore data, EmbedService embeds, IDatabase db)
        {
            _data = data;
            _embeds = embeds;
            _db = db;
        }

        public async Task Name(Context ctx)
        {
            ctx.CheckSystem();

            if (ctx.MatchFlag("c", "clear") || ctx.Match("clear"))
            {
                ctx.System.Name = null;
                await _data.SaveSystem(ctx.System);
                await ctx.Reply($"{Emojis.Success} System name cleared.");
                return;
            }

            var newSystemName = ctx.RemainderOrNull();
            if (newSystemName == null)
            {
                if (ctx.System.Name != null)
                    await ctx.Reply($"Your system's name is currently **{ctx.System.Name}**. Type `pk;system name -clear` to clear it.");
                else
                    await ctx.Reply("Your system currently does not have a name. Type `pk;system name <name>` to set one.");
                return;
            }
            
            if (newSystemName != null && newSystemName.Length > Limits.MaxSystemNameLength) throw Errors.SystemNameTooLongError(newSystemName.Length);
            ctx.System.Name = newSystemName;
            await _data.SaveSystem(ctx.System);
            await ctx.Reply($"{Emojis.Success} System name changed.");
        }
        
        public async Task Description(Context ctx) {
            ctx.CheckSystem();

            if (ctx.MatchFlag("c", "clear") || ctx.Match("clear"))
            {
                ctx.System.Description = null;
                await _data.SaveSystem(ctx.System);
                await ctx.Reply($"{Emojis.Success} System description cleared.");
                return;
            }
            
            var newDescription = ctx.RemainderOrNull()?.NormalizeLineEndSpacing();
            if (newDescription == null)
            {
                if (ctx.System.Description == null)
                    await ctx.Reply("Your system does not have a description set. To set one, type `pk;s description <description>`.");
                else if (ctx.MatchFlag("r", "raw"))
                    await ctx.Reply($"```\n{ctx.System.Description}\n```");
                else
                    await ctx.Reply(embed: new DiscordEmbedBuilder()
                        .WithTitle("System description")
                        .WithDescription(ctx.System.Description)
                        .WithFooter("To print the description with formatting, type `pk;s description -raw`. To clear it, type `pk;s description -clear`. To change it, type `pk;s description <new description>`.")
                        .Build());
            }
            else
            {
                if (newDescription.Length > Limits.MaxDescriptionLength) throw Errors.DescriptionTooLongError(newDescription.Length);
                ctx.System.Description = newDescription;
                await _data.SaveSystem(ctx.System);
                await ctx.Reply($"{Emojis.Success} System description changed.");
            }
        }
        
        public async Task Tag(Context ctx)
        {
            ctx.CheckSystem();

            if (ctx.MatchFlag("c", "clear") || ctx.Match("clear"))
            {
                ctx.System.TagSuffix = null;
                ctx.System.TagPrefix = null;
                await _data.SaveSystem(ctx.System);
                await ctx.Reply($"{Emojis.Success} System tag cleared.");
            } else if (!ctx.HasNext(skipFlags: false))
            {
                if (ctx.System.TagSuffix == null && ctx.System.TagPrefix == null)
                    await ctx.Reply($"You currently have no system tag. To set one, type `pk;s tag <tag>`.");
                else
                    await ctx.Reply($"Your current system tag is {(ctx.System.TagPrefix != null ? '`'+ctx.System.TagPrefix.EscapeMarkdown()+'`' : "")}**Name**{(ctx.System.TagSuffix != null ? '`'+ctx.System.TagSuffix.EscapeMarkdown()+'`' : "")}. To change it, type `pk;s tag <tag>`. To clear it, type `pk;s tag -clear`.");
            }
            else
            {
                var newTag = ctx.RemainderOrNull(skipFlags: false);
                var newTagPair = newTag.Split("text");
                if (newTag != null)
                    if(newTagPair.Length > 1){
                        if(newTagPair[0].Length + newTagPair[1].Length > Limits.MaxSystemTagLength){
                            throw Errors.SystemNameTooLongError(newTag.Length);
                        }
                        ctx.System.TagPrefix = (newTagPair[0].Trim() != "") ? newTagPair[0] : null;
                        ctx.System.TagSuffix = (newTagPair[1].Trim() != "") ? newTagPair[1] : null;
                    }
                    else if (newTag.Length > Limits.MaxSystemTagLength)
                        throw Errors.SystemNameTooLongError(newTag.Length);
                    else {
                        ctx.System.TagPrefix = null;
                        ctx.System.TagSuffix = " "+newTag;
                    }
                await _data.SaveSystem(ctx.System);
                await ctx.Reply($"{Emojis.Success} System tag changed. Member names will now {(ctx.System.TagPrefix!=null ? $"start with `{ctx.System.TagPrefix.EscapeMarkdown()}` ":"")}{(ctx.System.TagPrefix!=null&&ctx.System.TagSuffix!=null ? "and ":"")}{(ctx.System.TagSuffix!=null ? $"end with `{ctx.System.TagSuffix.EscapeMarkdown()}` ":"")}when proxied.");
            }
        }
        
        public async Task Avatar(Context ctx)
        {
            ctx.CheckSystem();
            
            if (ctx.Match("clear") || ctx.MatchFlag("c", "clear"))
            {
                ctx.System.AvatarUrl = null;
                await _data.SaveSystem(ctx.System);
                await ctx.Reply($"{Emojis.Success} System avatar cleared.");
                return;
            }
            else if (ctx.RemainderOrNull() == null && ctx.Message.Attachments.Count == 0)
            {
                if ((ctx.System.AvatarUrl?.Trim() ?? "").Length > 0)
                {
                    var eb = new DiscordEmbedBuilder()
                        .WithTitle($"System avatar")
                        .WithImageUrl(ctx.System.AvatarUrl)
                        .WithDescription($"To clear, use `pk;system avatar clear`.");
                    await ctx.Reply(embed: eb.Build());
                }
                else
                    throw new PKSyntaxError($"This system does not have an avatar set. Set one by attaching an image to this command, or by passing an image URL or @mention.");

                return;
            }

            var member = await ctx.MatchUser();
            if (member != null)
            {
                if (member.AvatarHash == null) throw Errors.UserHasNoAvatar;
                ctx.System.AvatarUrl = member.GetAvatarUrl(ImageFormat.Png, size: 256);
                await _data.SaveSystem(ctx.System);
            
                var embed = new DiscordEmbedBuilder().WithImageUrl(ctx.System.AvatarUrl).Build();
                await ctx.Reply(
                    $"{Emojis.Success} System avatar changed to {member.Username}'s avatar! {Emojis.Warn} Please note that if {member.Username} changes their avatar, the system's avatar will need to be re-set.", embed: embed);
            }
            else
            {
                // They can't both be null - otherwise we would've hit the conditional at the very top
                string url = ctx.RemainderOrNull() ?? ctx.Message.Attachments.FirstOrDefault()?.ProxyUrl;
                if (url?.Length > Limits.MaxUriLength) throw Errors.InvalidUrl(url);
                await ctx.BusyIndicator(() => AvatarUtils.VerifyAvatarOrThrow(url));

                ctx.System.AvatarUrl = url;
                await _data.SaveSystem(ctx.System);

                var embed = url != null ? new DiscordEmbedBuilder().WithImageUrl(url).Build() : null;
                await ctx.Reply($"{Emojis.Success} System avatar changed.", embed: embed);
            }
        }
        
        public async Task Delete(Context ctx) {
            ctx.CheckSystem();

            await ctx.Reply($"{Emojis.Warn} Are you sure you want to delete your system? If so, reply to this message with your system's ID (`{ctx.System.Hid}`).\n**Note: this action is permanent.**");
            if (!await ctx.ConfirmWithReply(ctx.System.Hid))
                throw new PKError($"System deletion cancelled. Note that you must reply with your system ID (`{ctx.System.Hid}`) *verbatim*.");

            await _data.DeleteSystem(ctx.System);
            await ctx.Reply($"{Emojis.Success} System deleted.");
        }
        
        public async Task SystemProxy(Context ctx)
        {
            ctx.CheckSystem().CheckGuildContext();
            var gs = await _db.Execute(c => c.QueryOrInsertSystemGuildConfig(ctx.Guild.Id, ctx.System.Id));

            bool newValue;
            if (ctx.Match("on", "enabled", "true", "yes")) newValue = true;
            else if (ctx.Match("off", "disabled", "false", "no")) newValue = false;
            else if (ctx.HasNext()) throw new PKSyntaxError("You must pass either \"on\" or \"off\".");
            else
            {
                if (gs.ProxyEnabled)
                    await ctx.Reply("Proxying in this server is currently **enabled** for your system. To disable it, type `pk;system proxy off`.");
                else
                    await ctx.Reply("Proxying in this server is currently **disabled** for your system. To enable it, type `pk;system proxy on`.");
                return;
            }

            await _db.Execute(c =>
                c.ExecuteAsync("update system_guild set proxy_enabled = @newValue where system = @system and guild = @guild",
                    new {newValue, system = ctx.System.Id, guild = ctx.Guild.Id}));

            if (newValue)
                await ctx.Reply($"Message proxying in this server ({ctx.Guild.Name.EscapeMarkdown()}) is now **enabled** for your system.");
            else
                await ctx.Reply($"Message proxying in this server ({ctx.Guild.Name.EscapeMarkdown()}) is now **disabled** for your system.");
        }
        
         public async Task SystemTimezone(Context ctx)
        {
            if (ctx.System == null) throw Errors.NoSystemError;

            if (ctx.MatchFlag("c", "clear") || ctx.Match("clear"))
            {
                ctx.System.UiTz = "UTC";
                await _data.SaveSystem(ctx.System);
                await ctx.Reply($"{Emojis.Success} System time zone cleared (set to UTC).");
                return;
            }
            
            var zoneStr = ctx.RemainderOrNull();
            if (zoneStr == null)
            {
                await ctx.Reply(
                    $"Your current system time zone is set to **{ctx.System.UiTz}**. It is currently **{SystemClock.Instance.GetCurrentInstant().FormatZoned(ctx.System)}** in that time zone. To change your system time zone, type `pk;s tz <zone>`.");
                return;
            }

            var zone = await FindTimeZone(ctx, zoneStr);
            if (zone == null) throw Errors.InvalidTimeZone(zoneStr);

            var currentTime = SystemClock.Instance.GetCurrentInstant().InZone(zone);
            var msg = await ctx.Reply(
                $"This will change the system time zone to **{zone.Id}**. The current time is **{currentTime.FormatZoned()}**. Is this correct?");
            if (!await ctx.PromptYesNo(msg)) throw Errors.TimezoneChangeCancelled;
            ctx.System.UiTz = zone.Id;
            await _data.SaveSystem(ctx.System);

            await ctx.Reply($"System time zone changed to **{zone.Id}**.");
        }

        public async Task SystemPrivacy(Context ctx)
        {
            ctx.CheckSystem();

            if (!ctx.HasNext())
            {
                string PrivacyLevelString(PrivacyLevel level) => level switch
                {
                    PrivacyLevel.Private => "**Private** (visible only when queried by you)",
                    PrivacyLevel.Public => "**Public** (visible to everyone)",
                    _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
                };

                var eb = new DiscordEmbedBuilder()
                    .WithTitle("Current privacy settings for your system")
                    .AddField("Description", PrivacyLevelString(ctx.System.DescriptionPrivacy))
                    .AddField("Member list", PrivacyLevelString(ctx.System.MemberListPrivacy))
                    .AddField("Current fronter(s)", PrivacyLevelString(ctx.System.FrontPrivacy))
                    .AddField("Front/switch history", PrivacyLevelString(ctx.System.FrontHistoryPrivacy))
                    .WithDescription("To edit privacy settings, use the command:\n`pk;system privacy <subject> <level>`\n\n- `subject` is one of `description`, `list`, `front`, `fronthistory`, or `all` \n- `level` is either `public` or `private`.");
                await ctx.Reply(embed: eb.Build());
                return;
            }
            
            PrivacyLevel PopPrivacyLevel(string subject, out string levelStr, out string levelExplanation)
            {
                if (ctx.Match("public", "show", "shown", "visible"))
                {
                    levelStr = "public";
                    levelExplanation = "be able to query";
                    return PrivacyLevel.Public;
                }

                if (ctx.Match("private", "hide", "hidden"))
                {
                    levelStr = "private";
                    levelExplanation = "*not* be able to query";
                    return PrivacyLevel.Private;
                }

                if (!ctx.HasNext())
                    throw new PKSyntaxError($"You must pass a privacy level for `{subject}` (`public` or `private`)");
                throw new PKSyntaxError($"Invalid privacy level `{ctx.PopArgument()}` (must be `public` or `private`).");
            }

            string levelStr, levelExplanation, subjectStr;
            var subjectList = "`description`, `members`, `front`, `fronthistory`, or `all`";
            if (ctx.Match("description", "desc", "text", "info"))
            {
                subjectStr = "description";
                ctx.System.DescriptionPrivacy = PopPrivacyLevel("description", out levelStr, out levelExplanation);
            } 
            else if (ctx.Match("members", "memberlist", "list", "mlist"))
            {
                subjectStr = "member list";
                ctx.System.MemberListPrivacy = PopPrivacyLevel("members", out levelStr, out levelExplanation);
            }
            else if (ctx.Match("front", "fronter"))
            {
                subjectStr = "fronter(s)";
                ctx.System.FrontPrivacy = PopPrivacyLevel("front", out levelStr, out levelExplanation);
            } 
            else if (ctx.Match("switch", "switches", "fronthistory", "fh"))
            {
                subjectStr = "front history";
                ctx.System.FrontHistoryPrivacy = PopPrivacyLevel("fronthistory", out levelStr, out levelExplanation);
            }
            else if (ctx.Match("all")){
                subjectStr = "all";
                PrivacyLevel level = PopPrivacyLevel("all", out levelStr, out levelExplanation);
                ctx.System.DescriptionPrivacy = level;
                ctx.System.MemberListPrivacy = level;
                ctx.System.FrontPrivacy = level;
                ctx.System.FrontHistoryPrivacy = level;

            }
            else
                throw new PKSyntaxError($"Invalid privacy subject `{ctx.PopArgument()}` (must be {subjectList}).");

            await _data.SaveSystem(ctx.System);
            if(subjectStr == "all"){
                if(levelStr == "private")
                    await ctx.Reply($"All of your systems privacy settings have been set to **{levelStr}**. Other accounts will now see nothing on the member card.");
                else 
                    await ctx.Reply($"All of your systems privacy have been set to **{levelStr}**. Other accounts will now see everything on the member card.");
            } 
            //Handle other subjects
            else
            await ctx.Reply($"System {subjectStr} privacy has been set to **{levelStr}**. Other accounts will now {levelExplanation} your system {subjectStr}.");
        }

        public async Task SystemPing(Context ctx) 
	    {
	        ctx.CheckSystem();

	        if (!ctx.HasNext()) 
	        {
		        if (ctx.System.PingsEnabled) {await ctx.Reply("Reaction pings are currently **enabled** for your system. To disable reaction pings, type `pk;s ping disable`.");}
		        else {await ctx.Reply("Reaction pings are currently **disabled** for your system. To enable reaction pings, type `pk;s ping enable`.");}
	        }
            else {
                if (ctx.Match("on", "enable")) {
                    ctx.System.PingsEnabled = true;
                    await _data.SaveSystem(ctx.System);
                    await ctx.Reply("Reaction pings have now been enabled.");
                }
                if (ctx.Match("off", "disable")) {
                    ctx.System.PingsEnabled = false;
                    await _data.SaveSystem(ctx.System);
                    await ctx.Reply("Reaction pings have now been disabled.");
                }
            }
	    }

        public async Task<DateTimeZone> FindTimeZone(Context ctx, string zoneStr) {
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
    }
}
