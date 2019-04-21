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

        public async Task<Embed> CreateEmbed(PKSystem system) {
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
    }
}