using System.Data;
using System.Threading.Tasks;
using Dapper;
using Discord;

namespace PluralKit.Bot {
    public class ServerDefinition {
        public ulong Id { get; set; }
        public ulong? LogChannel { get; set; } 
    }

    public class LogChannelService {
        private IDiscordClient _client;
        private DbConnectionFactory _conn;
        private EmbedService _embed;

        public LogChannelService(IDiscordClient client, DbConnectionFactory conn, EmbedService embed)
        {
            this._client = client;
            this._conn = conn;
            this._embed = embed;
        }

        public async Task LogMessage(PKSystem system, PKMember member, IMessage message, IUser sender) {
            var channel = await GetLogChannel((message.Channel as IGuildChannel).Guild);
            if (channel == null) return;

            var embed = _embed.CreateLoggedMessageEmbed(system, member, message, sender);
            await channel.SendMessageAsync(text: message.GetJumpUrl(), embed: embed);
        }

        public async Task<ITextChannel> GetLogChannel(IGuild guild) {
            using (var conn = await _conn.Obtain())
            {
                var server =
                    await conn.QueryFirstOrDefaultAsync<ServerDefinition>("select * from servers where id = @Id",
                        new {Id = guild.Id});
                if (server?.LogChannel == null) return null;
                return await _client.GetChannelAsync(server.LogChannel.Value) as ITextChannel;
            }
        }

        public async Task SetLogChannel(IGuild guild, ITextChannel newLogChannel) {
            var def = new ServerDefinition {
                Id = guild.Id,
                LogChannel = newLogChannel?.Id
            };

            using (var conn = await _conn.Obtain())
            {
                await conn.QueryAsync(
                    "insert into servers (id, log_channel) values (@Id, @LogChannel) on conflict (id) do update set log_channel = @LogChannel",
                    def);
            }
        }
    }
}