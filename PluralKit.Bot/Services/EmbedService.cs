using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using NodaTime;

namespace PluralKit.Bot {
    public class EmbedService {
        private SystemStore _systems;
        private MemberStore _members;
        private IDiscordClient _client;

        public EmbedService(SystemStore systems, MemberStore members, IDiscordClient client)
        {
            this._systems = systems;
            this._members = members;
            this._client = client;
        }

        public async Task<Embed> CreateSystemEmbed(PKSystem system) {
            var accounts = await _systems.GetLinkedAccountIds(system);

            // Fetch/render info for all accounts simultaneously
            var users = await Task.WhenAll(accounts.Select(async uid => (await _client.GetUserAsync(uid)).NameAndMention() ?? $"(deleted account {uid})"));

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
                .WithFooter($"System ID: {system.Hid} | Member ID: {member.Hid} | Sender: ${sender.Username}#{sender.Discriminator} ({sender.Id}) | Message ID: ${message.Id}")
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

        public Embed CreateFronterEmbed(PKSwitch sw, ICollection<PKMember> members, DateTimeZone zone)
        {
            var timeSinceSwitch = SystemClock.Instance.GetCurrentInstant() - sw.Timestamp;
            return new EmbedBuilder()
                .WithColor(members.FirstOrDefault()?.Color?.ToDiscordColor() ?? Color.Blue)
                .AddField("Current fronter", members.Count > 0 ? string.Join(", ", members.Select(m => m.Name)) : "*(no fronter)*", true)
                .AddField("Since", $"{Formats.ZonedDateTimeFormat.Format(sw.Timestamp.InZone(zone))} ({Formats.DurationFormat.Format(timeSinceSwitch)} ago)", true)
                .Build();
        }
    }
}