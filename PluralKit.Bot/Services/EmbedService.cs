using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

using Humanizer;
using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot {
    public class EmbedService
    {
        private IDataStore _data;
        private Database _db;
        private DiscordShardedClient _client;

        public EmbedService(DiscordShardedClient client, IDataStore data, Database db)
        {
            _client = client;
            _data = data;
            _db = db;
        }

        public async Task<DiscordEmbed> CreateSystemEmbed(DiscordClient client, PKSystem system, LookupContext ctx) {
            var accounts = await _data.GetSystemAccounts(system);

            // Fetch/render info for all accounts simultaneously
            var users = await Task.WhenAll(accounts.Select(async uid => (await client.GetUserAsync(uid))?.NameAndMention() ?? $"(deleted account {uid})"));

            var memberCount = await _data.GetSystemMemberCount(system, false);
            var eb = new DiscordEmbedBuilder()
                .WithColor(DiscordUtils.Gray)
                .WithTitle(system.Name ?? null)
                .WithThumbnailUrl(system.AvatarUrl)
                .WithFooter($"System ID: {system.Hid} | Created on {DateTimeFormats.ZonedDateTimeFormat.Format(system.Created.InZone(system.Zone))}");
 
            var latestSwitch = await _data.GetLatestSwitch(system);
            if (latestSwitch != null && system.FrontPrivacy.CanAccess(ctx))
            {
                var switchMembers = await _data.GetSwitchMembers(latestSwitch).ToListAsync();
                if (switchMembers.Count > 0)
                    eb.AddField("Fronter".ToQuantity(switchMembers.Count(), ShowQuantityAs.None),
                        string.Join(", ", switchMembers.Select(m => m.Name)));
            }

            if (system.Tag != null) eb.AddField("Tag", system.Tag.EscapeMarkdown());
            eb.AddField("Linked accounts", string.Join(", ", users).Truncate(1000), true);

            if (system.MemberListPrivacy.CanAccess(ctx))
            {
                if (memberCount > 0)
                    eb.AddField($"Members ({memberCount})", $"(see `pk;system {system.Hid} list` or `pk;system {system.Hid} list full`)", true);
                else
                    eb.AddField($"Members ({memberCount})", "Add one with `pk;member new`!", true);
            }

            if (system.Description != null && system.DescriptionPrivacy.CanAccess(ctx))
                eb.AddField("Description", system.Description.NormalizeLineEndSpacing().Truncate(1024), false);

            return eb.Build();
        }

        public DiscordEmbed CreateLoggedMessageEmbed(PKSystem system, PKMember member, ulong messageId, ulong originalMsgId, DiscordUser sender, string content, DiscordChannel channel) {
            // TODO: pronouns in ?-reacted response using this card
            var timestamp = DiscordUtils.SnowflakeToInstant(messageId);
            return new DiscordEmbedBuilder()
                .WithAuthor($"#{channel.Name}: {member.Name}", iconUrl: DiscordUtils.WorkaroundForUrlBug(member.AvatarUrl))
                .WithThumbnailUrl(member.AvatarUrl)
                .WithDescription(content?.NormalizeLineEndSpacing())
                .WithFooter($"System ID: {system.Hid} | Member ID: {member.Hid} | Sender: {sender.Username}#{sender.Discriminator} ({sender.Id}) | Message ID: {messageId} | Original Message ID: {originalMsgId}")
                .WithTimestamp(timestamp.ToDateTimeOffset())
                .Build();
        }

        public async Task<DiscordEmbed> CreateMemberEmbed(PKSystem system, PKMember member, DiscordGuild guild, LookupContext ctx)
        {
            var name = member.Name;
            if (system.Name != null) name = $"{member.Name} ({system.Name})";

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
            
            var guildSettings = guild != null ? await _db.Execute(c => c.QueryOrInsertMemberGuildConfig(guild.Id, member.Id)) : null;
            var guildDisplayName = guildSettings?.DisplayName;
            var avatar = guildSettings?.AvatarUrl ?? member.AvatarUrl;

            var proxyTagsStr = string.Join('\n', member.ProxyTags.Select(t => $"`{t.ProxyString}`"));

            var eb = new DiscordEmbedBuilder()
                // TODO: add URL of website when that's up
                .WithAuthor(name, iconUrl: DiscordUtils.WorkaroundForUrlBug(avatar))
                .WithColor(member.MemberPrivacy.CanAccess(ctx) ? color : DiscordUtils.Gray)
                .WithFooter($"System ID: {system.Hid} | Member ID: {member.Hid} | Created on {DateTimeFormats.ZonedDateTimeFormat.Format(member.Created.InZone(system.Zone))}");

            var description = "";
            if (member.MemberPrivacy == PrivacyLevel.Private) description += "*(this member is private)*\n";
            if (guildSettings?.AvatarUrl != null)
                if (member.AvatarUrl != null) 
                    description += $"*(this member has a server-specific avatar set; [click here]({member.AvatarUrl}) to see the global avatar)*\n";
                else
                    description += "*(this member has a server-specific avatar set)*\n";
            if (description != "") eb.WithDescription(description);

            if (avatar != null) eb.WithThumbnailUrl(avatar);

            if (!member.DisplayName.EmptyOrNull()) eb.AddField("Display Name", member.DisplayName.Truncate(1024), true);
            if (guild != null && guildDisplayName != null) eb.AddField($"Server Nickname (for {guild.Name})", guildDisplayName.Truncate(1024), true);
            if (member.Birthday != null && member.MemberPrivacy.CanAccess(ctx)) eb.AddField("Birthdate", member.BirthdayString, true);
            if (!member.Pronouns.EmptyOrNull() && member.MemberPrivacy.CanAccess(ctx)) eb.AddField("Pronouns", member.Pronouns.Truncate(1024), true);
            if (member.MessageCount > 0 && member.MemberPrivacy.CanAccess(ctx)) eb.AddField("Message Count", member.MessageCount.ToString(), true);
            if (member.HasProxyTags) eb.AddField("Proxy Tags", string.Join('\n', proxyTagsStr).Truncate(1024), true);
            if (!member.Color.EmptyOrNull() && member.MemberPrivacy.CanAccess(ctx)) eb.AddField("Color", $"#{member.Color}", true);
            if (!member.Description.EmptyOrNull() && member.MemberPrivacy.CanAccess(ctx)) eb.AddField("Description", member.Description.NormalizeLineEndSpacing(), false);

            return eb.Build();
        }

        public async Task<DiscordEmbed> CreateFronterEmbed(PKSwitch sw, DateTimeZone zone)
        {
            var members = await _data.GetSwitchMembers(sw).ToListAsync();
            var timeSinceSwitch = SystemClock.Instance.GetCurrentInstant() - sw.Timestamp;
            return new DiscordEmbedBuilder()
                .WithColor(members.FirstOrDefault()?.Color?.ToDiscordColor() ?? DiscordUtils.Gray)
                .AddField($"Current {"fronter".ToQuantity(members.Count, ShowQuantityAs.None)}", members.Count > 0 ? string.Join(", ", members.Select(m => m.Name)) : "*(no fronter)*")
                .AddField("Since", $"{DateTimeFormats.ZonedDateTimeFormat.Format(sw.Timestamp.InZone(zone))} ({DateTimeFormats.DurationFormat.Format(timeSinceSwitch)} ago)")
                .Build();
        }

        public async Task<DiscordEmbed> CreateMessageInfoEmbed(DiscordClient client, FullMessage msg)
        {
            var channel = await client.GetChannelAsync(msg.Message.Channel);
            var serverMsg = channel != null ? await channel.GetMessageAsync(msg.Message.Mid) : null;

            // Need this whole dance to handle cases where:
            // - the user is deleted (userInfo == null)
            // - the bot's no longer in the server we're querying (channel == null)
            // - the member is no longer in the server we're querying (memberInfo == null)
            DiscordMember memberInfo = null;
            DiscordUser userInfo = null; 
            if (channel != null) try { memberInfo = await channel.Guild.GetMemberAsync(msg.Message.Sender); } catch (NotFoundException) { }
            if (memberInfo != null) userInfo = memberInfo; // Don't do an extra request if we already have this info from the member lookup
            else try { userInfo = await client.GetUserAsync(msg.Message.Sender); } catch (NotFoundException) { }

            // Calculate string displayed under "Sent by"
            string userStr;
            if (memberInfo != null && memberInfo.Nickname != null)
                userStr = $"**Username:** {memberInfo.NameAndMention()}\n**Nickname:** {memberInfo.Nickname}";
            else if (userInfo != null) userStr = userInfo.NameAndMention();
            else userStr = $"*(deleted user {msg.Message.Sender})*";

            // Put it all together
            var eb = new DiscordEmbedBuilder()
                .WithAuthor(msg.Member.Name, iconUrl: DiscordUtils.WorkaroundForUrlBug(msg.Member.AvatarUrl))
                .WithDescription(serverMsg?.Content?.NormalizeLineEndSpacing() ?? "*(message contents deleted or inaccessible)*")
                .WithImageUrl(serverMsg?.Attachments?.FirstOrDefault()?.Url)
                .AddField("System",
                    msg.System.Name != null ? $"{msg.System.Name} (`{msg.System.Hid}`)" : $"`{msg.System.Hid}`", true)
                .AddField("Member", $"{msg.Member.Name} (`{msg.Member.Hid}`)", true)
                .AddField("Sent by", userStr, inline: true)
                .WithTimestamp(DiscordUtils.SnowflakeToInstant(msg.Message.Mid).ToDateTimeOffset());

            var roles = memberInfo?.Roles?.ToList();
            if (roles != null && roles.Count > 0)
                eb.AddField($"Account roles ({roles.Count})", string.Join(", ", roles.Select(role => role.Name)));
            
            return eb.Build();
        }

        public Task<DiscordEmbed> CreateFrontPercentEmbed(FrontBreakdown breakdown, DateTimeZone tz)
        {
            var actualPeriod = breakdown.RangeEnd - breakdown.RangeStart;
            var eb = new DiscordEmbedBuilder()
                .WithColor(DiscordUtils.Gray)
                .WithFooter($"Since {DateTimeFormats.ZonedDateTimeFormat.Format(breakdown.RangeStart.InZone(tz))} ({DateTimeFormats.DurationFormat.Format(actualPeriod)} ago)");

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
                eb.AddField(pair.Key?.Name ?? "*(no fronter)*", $"{frac*100:F0}% ({DateTimeFormats.DurationFormat.Format(pair.Value)})");
            }

            if (membersOrdered.Count > maxEntriesToDisplay)
            {
                eb.AddField("(others)",
                    DateTimeFormats.DurationFormat.Format(membersOrdered.Skip(maxEntriesToDisplay)
                        .Aggregate(Duration.Zero, (prod, next) => prod + next.Value)), true);
            }

            return Task.FromResult(eb.Build());
        }
    }
}