using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Humanizer;
using NodaTime;

namespace PluralKit.Bot {
    public class EmbedService {
        private SystemStore _systems;
        private MemberStore _members;
        private SwitchStore _switches;
        private MessageStore _messages;
        private IDiscordClient _client;

        public EmbedService(SystemStore systems, MemberStore members, IDiscordClient client, SwitchStore switches, MessageStore messages)
        {
            _systems = systems;
            _members = members;
            _client = client;
            _switches = switches;
            _messages = messages;
        }

        public async Task<Embed> CreateSystemEmbed(PKSystem system) {
            var accounts = await _systems.GetLinkedAccountIds(system);

            // Fetch/render info for all accounts simultaneously
            var users = await Task.WhenAll(accounts.Select(async uid => (await _client.GetUserAsync(uid))?.NameAndMention() ?? $"(deleted account {uid})"));

            var eb = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithTitle(system.Name ?? null)
                .WithDescription(system.Description?.Truncate(1024))
                .WithThumbnailUrl(system.AvatarUrl ?? null)
                .WithFooter($"System ID: {system.Hid}");

            eb.AddField("Linked accounts", string.Join(", ", users));
            eb.AddField("Members", $"(see `pk;system {system.Hid} list` or `pk;system {system.Hid} list full`)");
            // TODO: fronter
            return eb.Build();
        }

        public Embed CreateLoggedMessageEmbed(PKSystem system, PKMember member, IMessage message, IUser sender) {
            // TODO: pronouns in ?-reacted response using this card
            return new EmbedBuilder()
                .WithAuthor($"#{message.Channel.Name}: {member.Name}", member.AvatarUrl)
                .WithDescription(message.Content)
                .WithFooter($"System ID: {system.Hid} | Member ID: {member.Hid} | Sender: {sender.Username}#{sender.Discriminator} ({sender.Id}) | Message ID: {message.Id}")
                .WithTimestamp(message.Timestamp)
                .Build();
        }

        public async Task<Embed> CreateMemberEmbed(PKSystem system, PKMember member)
        {
            var name = member.Name;
            if (system.Name != null) name = $"{member.Name} ({system.Name})";
            
            var color = member.Color?.ToDiscordColor() ?? Color.Default;

            var messageCount = await _members.MessageCount(member);

            var eb = new EmbedBuilder()
                // TODO: add URL of website when that's up
                .WithAuthor(name, member.AvatarUrl)
                .WithColor(color)
                .WithDescription(member.Description)
                .WithFooter($"System ID: {system.Hid} | Member ID: {member.Hid}");

            if (member.Birthday != null) eb.AddField("Birthdate", member.BirthdayString);
            if (member.Pronouns != null) eb.AddField("Pronouns", member.Pronouns);
            if (messageCount > 0) eb.AddField("Message Count", messageCount);
            if (member.HasProxyTags) eb.AddField("Proxy Tags", $"{member.Prefix}text{member.Suffix}");

            return eb.Build();
        }

        public async Task<Embed> CreateFronterEmbed(PKSwitch sw, DateTimeZone zone)
        {
            var members = (await _switches.GetSwitchMembers(sw)).ToList();
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
                var members = (await _switches.GetSwitchMembers(sw)).ToList();
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

        public async Task<Embed> CreateMessageInfoEmbed(MessageStore.StoredMessage msg)
        {
            var channel = (ITextChannel) await _client.GetChannelAsync(msg.Message.Channel);
            var serverMsg = await channel.GetMessageAsync(msg.Message.Mid);

            var memberStr = $"{msg.Member.Name} (`{msg.Member.Hid}`)";
            if (msg.Member.Pronouns != null) memberStr += $"\n*(pronouns: **{msg.Member.Pronouns}**)*";
            
            return new EmbedBuilder()
                .WithAuthor(msg.Member.Name, msg.Member.AvatarUrl)
                .WithDescription(serverMsg?.Content ?? "*(message contents deleted or inaccessible)*")
                .AddField("System", msg.System.Name != null ? $"{msg.System.Name} (`{msg.System.Hid}`)" : $"`{msg.System.Hid}`", true)
                .AddField("Member", memberStr, true)
                .WithTimestamp(SnowflakeUtils.FromSnowflake(msg.Message.Mid))
                .Build();
        }

        public async Task<Embed> CreateFrontPercentEmbed(IDictionary<PKMember, Duration> frontpercent, ZonedDateTime startingFrom)
        {
            var totalDuration = SystemClock.Instance.GetCurrentInstant() - startingFrom.ToInstant();
            
            var eb = new EmbedBuilder()
                .WithColor(Color.Blue)
                .WithFooter($"Since {Formats.ZonedDateTimeFormat.Format(startingFrom)} ({Formats.DurationFormat.Format(totalDuration)} ago)");

            var maxEntriesToDisplay = 24; // max 25 fields allowed in embed - reserve 1 for "others"
            
            var membersOrdered = frontpercent.OrderBy(pair => pair.Value).Take(maxEntriesToDisplay).ToList();
            foreach (var pair in membersOrdered)
            {
                var frac = pair.Value / totalDuration;
                eb.AddField(pair.Key.Name, $"{frac*100:F0}% ({Formats.DurationFormat.Format(pair.Value)})");
            }

            if (membersOrdered.Count > maxEntriesToDisplay)
            {
                eb.AddField("(others)",
                    Formats.DurationFormat.Format(membersOrdered.Skip(maxEntriesToDisplay)
                        .Aggregate(Duration.Zero, (prod, next) => prod + next.Value)), true);
            }

            return eb.Build();
        }
    }
}