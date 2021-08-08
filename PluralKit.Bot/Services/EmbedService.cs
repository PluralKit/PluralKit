using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Humanizer;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Rest.Exceptions;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot {
    public class EmbedService
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly IDiscordCache _cache;
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
            await using var conn = await _db.Obtain();
            
            // Fetch/render info for all accounts simultaneously
            var accounts = await _repo.GetSystemAccounts(conn, system.Id);
            var users = (await GetUsers(accounts)).Select(x => x.User?.NameAndMention() ?? $"(deleted account {x.Id})");

            var memberCount = cctx.MatchPrivateFlag(ctx) ? await _repo.GetSystemMemberCount(conn, system.Id, PrivacyLevel.Public) : await _repo.GetSystemMemberCount(conn, system.Id);

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
                .Title(system.Name)
                .Thumbnail(new(system.AvatarUrl.TryGetCleanCdnUrl()))
                .Footer(new($"System ID: {system.Hid} | Created on {system.Created.FormatZoned(system)}"))
                .Color(color);

            if (system.DescriptionPrivacy.CanAccess(ctx))
                eb.Image(new(system.BannerImage));

            var latestSwitch = await _repo.GetLatestSwitch(conn, system.Id);
            if (latestSwitch != null && system.FrontPrivacy.CanAccess(ctx))
            {
                var switchMembers = await _repo.GetSwitchMembers(conn, latestSwitch.Id).ToListAsync();
                if (switchMembers.Count > 0)
                    eb.Field(new("Fronter".ToQuantity(switchMembers.Count, ShowQuantityAs.None), string.Join(", ", switchMembers.Select(m => m.NameFor(ctx)))));
            }

            if (system.Tag != null) 
                eb.Field(new("Tag", system.Tag.EscapeMarkdown(), true));

            if (cctx.Guild != null)
            {
                if (cctx.MessageContext.SystemGuildTag != null && cctx.MessageContext.TagEnabled)
                    eb.Field(new($"Tag (in server '{cctx.Guild.Name}')", cctx.MessageContext.SystemGuildTag
                        .EscapeMarkdown(), true));

                if (!cctx.MessageContext.TagEnabled)
                    eb.Field(new($"Tag (in server '{cctx.Guild.Name}')", "*(tag is disabled in this server)*"));
            }

            if (!system.Color.EmptyOrNull()) eb.Field(new("Color", $"#{system.Color}", true));

            eb.Field(new("Linked accounts", string.Join("\n", users).Truncate(1000), true));

            if (system.MemberListPrivacy.CanAccess(ctx))
            {
                if (memberCount > 0)
                    eb.Field(new($"Members ({memberCount})", $"(see `pk;system {system.Hid} list` or `pk;system {system.Hid} list full`)", true));
                else
                    eb.Field(new($"Members ({memberCount})", "Add one with `pk;member new`!", true));
            }

            if (system.DescriptionFor(ctx) is { } desc)
                eb.Field(new("Description", desc.NormalizeLineEndSpacing().Truncate(1024), false));

            return eb.Build();
        }

        public Embed CreateLoggedMessageEmbed(Message triggerMessage, Message proxiedMessage, string systemHid, PKMember member, string channelName, string oldContent = null) {
            // TODO: pronouns in ?-reacted response using this card
            var timestamp = DiscordUtils.SnowflakeToInstant(proxiedMessage.Id);
            var name = proxiedMessage.Author.Username;
            // sometimes Discord will just... not return the avatar hash with webhook messages
            var avatar = proxiedMessage.Author.Avatar != null ? proxiedMessage.Author.AvatarUrl() : member.AvatarFor(LookupContext.ByNonOwner);
            var embed = new EmbedBuilder()
                .Author(new($"#{channelName}: {name}", IconUrl: avatar))
                .Thumbnail(new(avatar))
                .Description(proxiedMessage.Content?.NormalizeLineEndSpacing())
                .Footer(new($"System ID: {systemHid} | Member ID: {member.Hid} | Sender: {triggerMessage.Author.Username}#{triggerMessage.Author.Discriminator} ({triggerMessage.Author.Id}) | Message ID: {proxiedMessage.Id} | Original Message ID: {triggerMessage.Id}"))
                .Timestamp(timestamp.ToDateTimeOffset().ToString("O"));

            if (oldContent != null)
                embed.Field(new("Old message", oldContent?.NormalizeLineEndSpacing().Truncate(1000)));
            
            return embed.Build();
        }

        public async Task<Embed> CreateMemberEmbed(PKSystem system, PKMember member, Guild guild, LookupContext ctx)
        {

            // string FormatTimestamp(Instant timestamp) => DateTimeFormats.ZonedDateTimeFormat.Format(timestamp.InZone(system.Zone));

            var name = member.NameFor(ctx);
            if (system.Name != null) name = $"{name} ({system.Name})";

            uint color;
            try
            {
                color = member.Color?.ToDiscordColor() ?? DiscordUtils.Gray;
            }
            catch (ArgumentException)
            {
                // Bad API use can cause an invalid color string
                // TODO: fix that in the API
                // for now we just default to a blank color, yolo
                color = DiscordUtils.Gray;
            }

            await using var conn = await _db.Obtain();
            
            var guildSettings = guild != null ? await _repo.GetMemberGuild(conn, guild.Id, member.Id) : null;
            var guildDisplayName = guildSettings?.DisplayName;
            var avatar = guildSettings?.AvatarUrl ?? member.AvatarFor(ctx);

            var groups = await _repo.GetMemberGroups(conn, member.Id)
                .Where(g => g.Visibility.CanAccess(ctx))
                .OrderBy(g => g.Name, StringComparer.InvariantCultureIgnoreCase)
                .ToListAsync();

            var eb = new EmbedBuilder()
                // TODO: add URL of website when that's up
                .Author(new(name, IconUrl: avatar.TryGetCleanCdnUrl()))
                // .WithColor(member.ColorPrivacy.CanAccess(ctx) ? color : DiscordUtils.Gray)
                .Color(color)
                .Footer(new(
                    $"System ID: {system.Hid} | Member ID: {member.Hid} {(member.MetadataPrivacy.CanAccess(ctx) ? $"| Created on {member.Created.FormatZoned(system)}" : "")}"));

            if (member.DescriptionPrivacy.CanAccess(ctx))
                eb.Image(new(member.BannerImage));

            var description = "";
            if (member.MemberVisibility == PrivacyLevel.Private) description += "*(this member is hidden)*\n";
            if (guildSettings?.AvatarUrl != null)
                if (member.AvatarFor(ctx) != null) 
                    description += $"*(this member has a server-specific avatar set; [click here]({member.AvatarUrl.TryGetCleanCdnUrl()}) to see the global avatar)*\n";
                else
                    description += "*(this member has a server-specific avatar set)*\n";
            if (description != "") eb.Description(description);
            
            if (avatar != null) eb.Thumbnail(new(avatar.TryGetCleanCdnUrl()));

            if (!member.DisplayName.EmptyOrNull() && member.NamePrivacy.CanAccess(ctx)) eb.Field(new("Display Name", member.DisplayName.Truncate(1024), true));
            if (guild != null && guildDisplayName != null) eb.Field(new($"Server Nickname (for {guild.Name})", guildDisplayName.Truncate(1024), true));
            if (member.BirthdayFor(ctx) != null) eb.Field(new("Birthdate", member.BirthdayString, true));
            if (member.PronounsFor(ctx) is {} pronouns && !string.IsNullOrWhiteSpace(pronouns)) eb.Field(new("Pronouns", pronouns.Truncate(1024), true));
            if (member.MessageCountFor(ctx) is {} count && count > 0) eb.Field(new("Message Count", member.MessageCount.ToString(), true));
            if (member.HasProxyTags) eb.Field(new("Proxy Tags", member.ProxyTagsString("\n").Truncate(1024), true));
            // --- For when this gets added to the member object itself or however they get added
            // if (member.LastMessage != null && member.MetadataPrivacy.CanAccess(ctx)) eb.AddField("Last message:" FormatTimestamp(DiscordUtils.SnowflakeToInstant(m.LastMessage.Value)));
            // if (member.LastSwitchTime != null && m.MetadataPrivacy.CanAccess(ctx)) eb.AddField("Last switched in:", FormatTimestamp(member.LastSwitchTime.Value));
            // if (!member.Color.EmptyOrNull() && member.ColorPrivacy.CanAccess(ctx)) eb.AddField("Color", $"#{member.Color}", true);
            if (!member.Color.EmptyOrNull()) eb.Field(new("Color", $"#{member.Color}", true));

            if (groups.Count > 0)
            {
                // More than 5 groups show in "compact" format without ID
                var content = groups.Count > 5
                    ? string.Join(", ", groups.Select(g => g.DisplayName ?? g.Name))
                    : string.Join("\n", groups.Select(g => $"[`{g.Hid}`] **{g.DisplayName ?? g.Name}**"));
                eb.Field(new($"Groups ({groups.Count})", content.Truncate(1000)));
            }

            if (member.DescriptionFor(ctx) is {} desc) 
                eb.Field(new("Description", member.Description.NormalizeLineEndSpacing(), false));

            return eb.Build();
        }

        public async Task<Embed> CreateGroupEmbed(Context ctx, PKSystem system, PKGroup target)
        {
            await using var conn = await _db.Obtain();
            
            var pctx = ctx.LookupContextFor(system);
            var memberCount = ctx.MatchPrivateFlag(pctx) ? await _repo.GetGroupMemberCount(conn, target.Id, PrivacyLevel.Public) : await _repo.GetGroupMemberCount(conn, target.Id);

            var nameField = target.Name;
            if (system.Name != null)
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
                .Author(new(nameField, IconUrl: target.IconFor(pctx)))
                .Color(color)
                .Footer(new($"System ID: {system.Hid} | Group ID: {target.Hid} | Created on {target.Created.FormatZoned(system)}"));

            if (target.DescriptionPrivacy.CanAccess(ctx.LookupContextFor(target.System)))
                eb.Image(new(target.BannerImage));

            if (target.DisplayName != null)
                eb.Field(new("Display Name", target.DisplayName, true));
                
            if (!target.Color.EmptyOrNull()) eb.Field(new("Color", $"#{target.Color}", true));

            if (target.ListPrivacy.CanAccess(pctx))
            {
                if (memberCount == 0 && pctx == LookupContext.ByOwner)
                    // Only suggest the add command if this is actually the owner lol
                    eb.Field(new("Members (0)", $"Add one with `pk;group {target.Reference()} add <member>`!", false));
                else
                    eb.Field(new($"Members ({memberCount})", $"(see `pk;group {target.Reference()} list`)", false));
            }

            if (target.DescriptionFor(pctx) is { } desc)
                eb.Field(new("Description", desc));

            if (target.IconFor(pctx) is {} icon)
                eb.Thumbnail(new(icon.TryGetCleanCdnUrl()));

            return eb.Build();
        }

        public async Task<Embed> CreateFronterEmbed(PKSwitch sw, DateTimeZone zone, LookupContext ctx)
        {
            var members = await _db.Execute(c => _repo.GetSwitchMembers(c, sw.Id).ToListAsync().AsTask());
            var timeSinceSwitch = SystemClock.Instance.GetCurrentInstant() - sw.Timestamp;
            return new EmbedBuilder()
                .Color(members.FirstOrDefault()?.Color?.ToDiscordColor() ?? DiscordUtils.Gray)
                .Field(new($"Current {"fronter".ToQuantity(members.Count, ShowQuantityAs.None)}", members.Count > 0 ? string.Join(", ", members.Select(m => m.NameFor(ctx))) : "*(no fronter)*"))
                .Field(new("Since", $"{sw.Timestamp.FormatZoned(zone)} ({timeSinceSwitch.FormatDuration()} ago)"))
                .Build();
        }

        public async Task<Embed> CreateMessageInfoEmbed(FullMessage msg)
        {
            var channel = await _cache.GetOrFetchChannel(_rest, msg.Message.Channel);
            var ctx = LookupContext.ByNonOwner;

            Message serverMsg = null;
            try
            {
                serverMsg = await _rest.GetMessage(msg.Message.Channel, msg.Message.Mid);
            }
            catch (ForbiddenException)
            {
                // no permission, couldn't fetch, oh well
            }

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
            if (memberInfo != null && memberInfo.Nick != null)
                userStr = $"**Username:** {userInfo.NameAndMention()}\n**Nickname:** {memberInfo.Nick}";
            else if (userInfo != null) userStr = userInfo.NameAndMention();
            else userStr = $"*(deleted user {msg.Message.Sender})*";

            // Put it all together
            var eb = new EmbedBuilder()
                .Author(new(msg.Member.NameFor(ctx), IconUrl: msg.Member.AvatarFor(ctx).TryGetCleanCdnUrl()))
                .Description(serverMsg?.Content?.NormalizeLineEndSpacing() ?? "*(message contents deleted or inaccessible)*")
                .Image(new(serverMsg?.Attachments?.FirstOrDefault()?.Url))
                .Field(new("System",
                    msg.System.Name != null ? $"{msg.System.Name} (`{msg.System.Hid}`)" : $"`{msg.System.Hid}`", true))
                .Field(new("Member", $"{msg.Member.NameFor(ctx)} (`{msg.Member.Hid}`)", true))
                .Field(new("Sent by", userStr, true))
                .Timestamp(DiscordUtils.SnowflakeToInstant(msg.Message.Mid).ToDateTimeOffset().ToString("O"));

            var roles = memberInfo?.Roles?.ToList();
            if (roles != null && roles.Count > 0)
            {
                // TODO: what if role isn't in cache? figure out a fallback
                var rolesString = string.Join(", ", roles
                    .Select(id => _cache.GetRole(id))
                    .OrderByDescending(role => role.Position)
                    .Select(role => role.Name));
                eb.Field(new($"Account roles ({roles.Count})", rolesString.Truncate(1024)));
            }
            
            return eb.Build();
        }

        public Task<Embed> CreateFrontPercentEmbed(FrontBreakdown breakdown, PKSystem system, PKGroup group, DateTimeZone tz, LookupContext ctx, string embedTitle, bool ignoreNoFronters, bool showFlat)
        {
            string color = system.Color;
            if (group != null) 
            {
                color = group.Color;
            }

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

            string footer = $"Since {breakdown.RangeStart.FormatZoned(tz)} ({(breakdown.RangeEnd - breakdown.RangeStart).FormatDuration()} ago)";

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
                period = breakdown.RangeEnd - breakdown.RangeStart;

            eb.Footer(new(footer));

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
                eb.Field(new(pair.Key?.NameFor(ctx) ?? "*(no fronter)*", $"{frac*100:F0}% ({pair.Value.FormatDuration()})"));
            }

            if (membersOrdered.Count > maxEntriesToDisplay)
            {
                eb.Field(new("(others)",
                    membersOrdered.Skip(maxEntriesToDisplay)
                        .Aggregate(Duration.Zero, (prod, next) => prod + next.Value)
                        .FormatDuration(), true));
            }

            return Task.FromResult(eb.Build());
        }
    }
}
