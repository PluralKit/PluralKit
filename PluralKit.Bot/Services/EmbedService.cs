using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace PluralKit.Bot {
    public class EmbedService {
        private SystemStore _systems;
        private IDiscordClient _client;

        public EmbedService(SystemStore systems, IDiscordClient client)
        {
            this._systems = systems;
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
    }
}