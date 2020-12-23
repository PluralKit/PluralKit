using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;

using Humanizer;

using Myriad.Builders;
using Myriad.Cache;
using Myriad.Rest;
using Myriad.Types;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot {
    public class EmbedService
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly DiscordShardedClient _client;
        private readonly IDiscordCache _cache;
        private readonly DiscordApiClient _rest;

        public EmbedService(DiscordShardedClient client, IDatabase db, ModelRepository repo, IDiscordCache cache, DiscordApiClient rest)
        {
            _client = client;
            _db = db;
            _repo = repo;
            _cache = cache;
            _rest = rest;
        }

        private Task<(ulong Id, User? User)[]> GetUsers(IEnumerable<ulong> ids)
        {
            async Task<(ulong Id, User? User)> Inner(ulong id)
            {
                if (_cache.TryGetUser(id, out var cachedUser))
                    return (id, cachedUser);
                
                var user = await _rest.GetUser(id);
                if (user == null)
                    return (id, null);
                // todo: move to "GetUserCached" helper
                await _cache.SaveUser(user);
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

            var eb = new EmbedBuilder()
                .Title(system.Name)
                .Thumbnail(new(system.AvatarUrl))
                .Footer(new($"System ID: {system.Hid} | Created on {system.Created.FormatZoned(system)}"))
                .Color((uint) DiscordUtils.Gray.Value);

            var latestSwitch = await _repo.GetLatestSwitch(conn, system.Id);
            if (latestSwitch != null && system.FrontPrivacy.CanAccess(ctx))
            {
                var switchMembers = await _repo.GetSwitchMembers(conn, latestSwitch.Id).ToListAsync();
                if (switchMembers.Count > 0)
                    eb.Field(new("Fronter".ToQuantity(switchMembers.Count, ShowQuantityAs.None), string.Join(", ", switchMembers.Select(m => m.NameFor(ctx)))));
            }

            if (system.Tag != null) 
                eb.Field(new("Tag", system.Tag.EscapeMarkdown()));
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

        public Embed CreateLoggedMessageEmbed(PKSystem system, PKMember member, ulong messageId, ulong originalMsgId, User sender, string content, Channel channel) {
            // TODO: pronouns in ?-reacted response using this card
            var timestamp = DiscordUtils.SnowflakeToInstant(messageId);
            var name = member.NameFor(LookupContext.ByNonOwner); 
            return new EmbedBuilder()
                .Author(new($"#{channel.Name}: {name}", IconUrl: DiscordUtils.WorkaroundForUrlBug(member.AvatarFor(LookupContext.ByNonOwner))))
                .Thumbnail(new(member.AvatarFor(LookupContext.ByNonOwner)))
                .Description(content?.NormalizeLineEndSpacing())
                .Footer(new($"System ID: {system.Hid} | Member ID: {member.Hid} | Sender: {sender.Username}#{sender.Discriminator} ({sender.Id}) | Message ID: {messageId} | Original Message ID: {originalMsgId}"))
                .Timestamp(timestamp.ToDateTimeOffset().ToString("O"))
                .Build();
        }

        public async Task<Embed> CreateMemberEmbed(PKSystem system, PKMember member, DiscordGuild guild, LookupContext ctx)
        {

            // string FormatTimestamp(Instant timestamp) => DateTimeFormats.ZonedDateTimeFormat.Format(timestamp.InZone(system.Zone));

            var name = member.NameFor(ctx);
            if (system.Name != null) name = $"{name} ({system.Name})";

            DiscordColor color;
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
                .Author(new(name, IconUrl: DiscordUtils.WorkaroundForUrlBug(avatar)))
                // .WithColor(member.ColorPrivacy.CanAccess(ctx) ? color : DiscordUtils.Gray)
                .Color((uint?) color.Value)
                .Footer(new(
                    $"System ID: {system.Hid} | Member ID: {member.Hid} {(member.MetadataPrivacy.CanAccess(ctx) ? $"| Created on {member.Created.FormatZoned(system)}" : "")}"));

            var description = "";
            if (member.MemberVisibility == PrivacyLevel.Private) description += "*(this member is hidden)*\n";
            if (guildSettings?.AvatarUrl != null)
                if (member.AvatarFor(ctx) != null) 
                    description += $"*(this member has a server-specific avatar set; [click here]({member.AvatarUrl}) to see the global avatar)*\n";
                else
                    description += "*(this member has a server-specific avatar set)*\n";
            if (description != "") eb.Description(description);
            
            if (avatar != null) eb.Thumbnail(new(avatar));

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

            var eb = new EmbedBuilder()
                .Author(new(nameField, IconUrl: DiscordUtils.WorkaroundForUrlBug(target.IconFor(pctx))))
                .Footer(new($"System ID: {system.Hid} | Group ID: {target.Hid} | Created on {target.Created.FormatZoned(system)}"));

            if (target.DisplayName != null)
                eb.Field(new("Display Name", target.DisplayName));

            if (target.ListPrivacy.CanAccess(pctx))
            {
                if (memberCount == 0 && pctx == LookupContext.ByOwner)
                    // Only suggest the add command if this is actually the owner lol
                    eb.Field(new("Members (0)", $"Add one with `pk;group {target.Reference()} add <member>`!", true));
                else
                    eb.Field(new($"Members ({memberCount})", $"(see `pk;group {target.Reference()} list`)", true));
            }

            if (target.DescriptionFor(pctx) is { } desc)
                eb.Field(new("Description", desc));

            if (target.IconFor(pctx) is {} icon)
                eb.Thumbnail(new(icon));

            return eb.Build();
        }

        public async Task<Embed> CreateFronterEmbed(PKSwitch sw, DateTimeZone zone, LookupContext ctx)
        {
            var members = await _db.Execute(c => _repo.GetSwitchMembers(c, sw.Id).ToListAsync().AsTask());
            var timeSinceSwitch = SystemClock.Instance.GetCurrentInstant() - sw.Timestamp;
            return new EmbedBuilder()
                .Color((uint?) (members.FirstOrDefault()?.Color?.ToDiscordColor()?.Value ?? DiscordUtils.Gray.Value))
                .Field(new($"Current {"fronter".ToQuantity(members.Count, ShowQuantityAs.None)}", members.Count > 0 ? string.Join(", ", members.Select(m => m.NameFor(ctx))) : "*(no fronter)*"))
                .Field(new("Since", $"{sw.Timestamp.FormatZoned(zone)} ({timeSinceSwitch.FormatDuration()} ago)"))
                .Build();
        }

        public async Task<Embed> CreateMessageInfoEmbed(DiscordClient client, FullMessage msg)
        {
            var ctx = LookupContext.ByNonOwner;
            var channel = await _client.GetChannel(msg.Message.Channel);
            var serverMsg = channel != null ? await channel.GetMessage(msg.Message.Mid) : null;

            // Need this whole dance to handle cases where:
            // - the user is deleted (userInfo == null)
            // - the bot's no longer in the server we're querying (channel == null)
            // - the member is no longer in the server we're querying (memberInfo == null)
            DiscordMember memberInfo = null;
            DiscordUser userInfo = null;
            if (channel != null) memberInfo = await channel.Guild.GetMember(msg.Message.Sender);
            if (memberInfo != null) userInfo = memberInfo; // Don't do an extra request if we already have this info from the member lookup
            else userInfo = await client.GetUser(msg.Message.Sender);

            // Calculate string displayed under "Sent by"
            string userStr;
            if (memberInfo != null && memberInfo.Nickname != null)
                userStr = $"**Username:** {memberInfo.NameAndMention()}\n**Nickname:** {memberInfo.Nickname}";
            else if (userInfo != null) userStr = userInfo.NameAndMention();
            else userStr = $"*(deleted user {msg.Message.Sender})*";

            // Put it all together
            var eb = new EmbedBuilder()
                .Author(new(msg.Member.NameFor(ctx), IconUrl: DiscordUtils.WorkaroundForUrlBug(msg.Member.AvatarFor(ctx))))
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
                var rolesString = string.Join(", ", roles.Select(role => role.Name));
                eb.Field(new($"Account roles ({roles.Count})", rolesString.Truncate(1024)));
            }
            
            return eb.Build();
        }

        public Task<Embed> CreateFrontPercentEmbed(FrontBreakdown breakdown, DateTimeZone tz, LookupContext ctx)
        {
            var actualPeriod = breakdown.RangeEnd - breakdown.RangeStart;
            var eb = new EmbedBuilder()
                .Color((uint?) DiscordUtils.Gray.Value)
                .Footer(new($"Since {breakdown.RangeStart.FormatZoned(tz)} ({actualPeriod.FormatDuration()} ago)"));

            var maxEntriesToDisplay = 24; // max 25 fields allowed in embed - reserve 1 for "others"

            // We convert to a list of pairs so we can add the no-fronter value
            // Dictionary doesn't allow for null keys so we instead have a pair with a null key ;)
            var pairs = breakdown.MemberSwitchDurations.ToList();
            if (breakdown.NoFronterDuration != Duration.Zero)
                pairs.Add(new KeyValuePair<PKMember, Duration>(null, breakdown.NoFronterDuration));

            var membersOrdered = pairs.OrderByDescending(pair => pair.Value).Take(maxEntriesToDisplay).ToList();
            foreach (var pair in membersOrdered)
            {
                var frac = pair.Value / actualPeriod;
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
