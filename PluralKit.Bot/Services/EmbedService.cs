using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

using Humanizer;
using NodaTime;

namespace PluralKit.Bot {
    public class EmbedService
    {
        private IDataStore _data;
        private IDiscordClient _client;

        public EmbedService(IDiscordClient client, IDataStore data)
        {
            _client = client;
            _data = data;
        }

        public async Task<Embed> CreateSystemEmbed(PKSystem system) {
            var accounts = await _data.GetSystemAccounts(system);

            // Fetch/render info for all accounts simultaneously
            var users = await Task.WhenAll(accounts.Select(async uid => (await _client.GetUserAsync(uid))?.NameAndMention() ?? $"(deleted account {uid})"));

            var memberCount = await _data.GetSystemMemberCount(system);
            var eb = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle(system.Name ?? null)
                .WithThumbnailUrl(system.AvatarUrl ?? null)
                .WithFooter($"System ID: {system.Hid} | Created on {Formats.ZonedDateTimeFormat.Format(system.Created.InZone(system.Zone))}");
 
            var latestSwitch = await _data.GetLatestSwitch(system);
            if (latestSwitch != null)
            {
                var switchMembers = (await _data.GetSwitchMembers(latestSwitch)).ToList();
                if (switchMembers.Count > 0)
                    eb.AddField("Fronter".ToQuantity(switchMembers.Count(), ShowQuantityAs.None),
                        string.Join(", ", switchMembers.Select(m => m.Name)));
            }

            if (system.Tag != null) eb.AddField("Tag", system.Tag);
            eb.AddField("Linked accounts", string.Join(", ", users), true);
            eb.AddField($"Members ({memberCount})", $"(see `pk;system {system.Hid} list` or `pk;system {system.Hid} list full`)", true);

            if (system.Description != null) eb.AddField("Description", system.Description.Truncate(1024), false);

            return eb.Build();
        }

        public Embed CreateLoggedMessageEmbed(PKSystem system, PKMember member, ulong messageId, ulong originalMsgId, IUser sender, string content, IGuildChannel channel) {
            // TODO: pronouns in ?-reacted response using this card
            var timestamp = SnowflakeUtils.FromSnowflake(messageId);
            return new EmbedBuilder()
                .WithAuthor($"#{channel.Name}: {member.Name}", member.AvatarUrl)
                .WithDescription(content)
                .WithFooter($"System ID: {system.Hid} | Member ID: {member.Hid} | Sender: {sender.Username}#{sender.Discriminator} ({sender.Id}) | Message ID: {messageId} | Original Message ID: {originalMsgId}")
                .WithTimestamp(timestamp)
                .Build();
        }

        public async Task<Embed> CreateMemberEmbed(PKSystem system, PKMember member)
        {
            var name = member.Name;
            if (system.Name != null) name = $"{member.Name} ({system.Name})";

            Color color;
            try
            {
                color = member.Color?.ToDiscordColor() ?? Color.Default;
            }
            catch (ArgumentException)
            {
                // Bad API use can cause an invalid color string
                // TODO: fix that in the API
                // for now we just default to a blank color, yolo
                color = Color.Default;
            }

            var messageCount = await _data.GetMemberMessageCount(member);

            var eb = new EmbedBuilder()
                // TODO: add URL of website when that's up
                .WithAuthor(name, member.AvatarUrl)
                .WithColor(color)
                .WithFooter($"System ID: {system.Hid} | Member ID: {member.Hid} | Created on {Formats.ZonedDateTimeFormat.Format(member.Created.InZone(system.Zone))}");

            if (member.AvatarUrl != null) eb.WithThumbnailUrl(member.AvatarUrl);

            if (member.DisplayName != null) eb.AddField("Display Name", member.DisplayName, true);
            if (member.Birthday != null) eb.AddField("Birthdate", member.BirthdayString, true);
            if (member.Pronouns != null) eb.AddField("Pronouns", member.Pronouns, true);
            if (messageCount > 0) eb.AddField("Message Count", messageCount, true);
            if (member.HasProxyTags) eb.AddField("Proxy Tags", $"{member.Prefix.EscapeMarkdown()}text{member.Suffix.EscapeMarkdown()}", true);
            if (member.Color != null) eb.AddField("Color", $"#{member.Color}", true);
            if (member.Description != null) eb.AddField("Description", member.Description, false);

            return eb.Build();
        }

        public async Task<Embed> CreateFronterEmbed(PKSwitch sw, DateTimeZone zone)
        {
            var members = (await _data.GetSwitchMembers(sw)).ToList();
            var timeSinceSwitch = SystemClock.Instance.GetCurrentInstant() - sw.Timestamp;
            return new EmbedBuilder()
                .WithColor(members.FirstOrDefault()?.Color?.ToDiscordColor() ?? Color.Blue)
                .AddField($"Current {"fronter".ToQuantity(members.Count, ShowQuantityAs.None)}", members.Count > 0 ? string.Join(", ", members.Select(m => m.Name)) : "*(no fronter)*")
                .AddField("Since", $"{Formats.ZonedDateTimeFormat.Format(sw.Timestamp.InZone(zone))} ({Formats.DurationFormat.Format(timeSinceSwitch)} ago)")
                .Build();
        }

        public async Task<Embed> CreateFrontHistoryEmbed(IEnumerable<PKSwitch> sws, DateTimeZone zone)
        {
            var outputStr = "";

            PKSwitch lastSw = null;
            foreach (var sw in sws)
            {
                // Fetch member list and format
                var members = (await _data.GetSwitchMembers(sw)).ToList();
                var membersStr = members.Any() ? string.Join(", ", members.Select(m => m.Name)) : "no fronter";

                var switchSince = SystemClock.Instance.GetCurrentInstant() - sw.Timestamp;

                // If this isn't the latest switch, we also show duration
                if (lastSw != null)
                {
                    // Calculate the time between the last switch (that we iterated - ie. the next one on the timeline) and the current one
                    var switchDuration = lastSw.Timestamp - sw.Timestamp;
                    outputStr += $"**{membersStr}** ({Formats.ZonedDateTimeFormat.Format(sw.Timestamp.InZone(zone))}, {Formats.DurationFormat.Format(switchSince)} ago, for {Formats.DurationFormat.Format(switchDuration)})\n";
                }
                else
                {
                    outputStr += $"**{membersStr}** ({Formats.ZonedDateTimeFormat.Format(sw.Timestamp.InZone(zone))}, {Formats.DurationFormat.Format(switchSince)} ago)\n";
                }

                lastSw = sw;
            }

            return new EmbedBuilder()
                .WithTitle("Past switches")
                .WithDescription(outputStr)
                .Build();
        }

        public async Task<Embed> CreateMessageInfoEmbed(FullMessage msg)
        {
            var channel = await _client.GetChannelAsync(msg.Message.Channel) as ITextChannel;
            var serverMsg = channel != null ? await channel.GetMessageAsync(msg.Message.Mid) : null;

            var memberStr = $"{msg.Member.Name} (`{msg.Member.Hid}`)";

            var userStr = $"*(deleted user {msg.Message.Sender})*";
            ICollection<IRole> roles = null;

            if (channel != null)
            {
                // Look up the user with the REST client
                // this ensures we'll still get the information even if the user's not cached,
                // even if this means an extra API request (meh, it'll be fine)
                var shard = ((DiscordShardedClient) _client).GetShardFor(channel.Guild);
                var guildUser = await shard.Rest.GetGuildUserAsync(channel.Guild.Id, msg.Message.Sender);
                if (guildUser != null)
                {
                    roles = guildUser.RoleIds
                        .Select(roleId => channel.Guild.GetRole(roleId))
                        .Where(role => role.Name != "@everyone")
                        .OrderByDescending(role => role.Position)
                        .ToList();

                    userStr = guildUser.Nickname != null ? $"**Username:** {guildUser?.NameAndMention()}\n**Nickname:** {guildUser.Nickname}" : guildUser?.NameAndMention();
                }
            }

            var eb = new EmbedBuilder()
                .WithAuthor(msg.Member.Name, msg.Member.AvatarUrl)
                .WithDescription(serverMsg?.Content ?? "*(message contents deleted or inaccessible)*")
                .WithImageUrl(serverMsg?.Attachments?.First()?.Url)
                .AddField("System",
                    msg.System.Name != null ? $"{msg.System.Name} (`{msg.System.Hid}`)" : $"`{msg.System.Hid}`", true)
                .AddField("Member", memberStr, true)
                .AddField("Sent by", userStr, inline: true)
                .WithTimestamp(SnowflakeUtils.FromSnowflake(msg.Message.Mid));

            if (roles != null && roles.Count > 0)
                eb.AddField($"Account roles ({roles.Count})", string.Join(", ", roles.Select(role => role.Name)));
            return eb.Build();
        }

        public Task<Embed> CreateFrontPercentEmbed(FrontBreakdown breakdown, DateTimeZone tz)
        {
            var actualPeriod = breakdown.RangeEnd - breakdown.RangeStart;
            var eb = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithFooter($"Since {Formats.ZonedDateTimeFormat.Format(breakdown.RangeStart.InZone(tz))} ({Formats.DurationFormat.Format(actualPeriod)} ago)");

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
                eb.AddField(pair.Key?.Name ?? "*(no fronter)*", $"{frac*100:F0}% ({Formats.DurationFormat.Format(pair.Value)})");
            }

            if (membersOrdered.Count > maxEntriesToDisplay)
            {
                eb.AddField("(others)",
                    Formats.DurationFormat.Format(membersOrdered.Skip(maxEntriesToDisplay)
                        .Aggregate(Duration.Zero, (prod, next) => prod + next.Value)), true);
            }

            return Task.FromResult(eb.Build());
        }
    }
}