using System.Data;
using System.Threading.Tasks;
using Dapper;
using Discord;

namespace PluralKit.Bot {
    class ServerDefinition {
        public ulong Id;
        public ulong LogChannel;
    }

    class LogChannelService {
        private IDiscordClient _client;
        private IDbConnection _connection;
        private EmbedService _embed;

        public LogChannelService(IDiscordClient client, IDbConnection connection, EmbedService embed)
        {
            this._client = client;
            this._connection = connection;
            this._embed = embed;
        }

        public async Task LogMessage(PKSystem system, PKMember member, IMessage message, IUser sender) {
            var channel = await GetLogChannel((message.Channel as IGuildChannel).Guild);
            if (channel == null) return;

            var embed = _embed.CreateLoggedMessageEmbed(system, member, message, sender);
            await channel.SendMessageAsync(text: message.GetJumpUrl(), embed: embed);
        }

        public async Task<ITextChannel> GetLogChannel(IGuild guild) {
            var server = await _connection.QueryFirstAsync<ServerDefinition>("select * from servers where id = @Id", new { Id = guild.Id });
            if (server == null) return null;
            return await _client.GetChannelAsync(server.LogChannel) as ITextChannel;
        }

        public async Task SetLogChannel(IGuild guild, ITextChannel newLogChannel) {
            var def = new ServerDefinition {
                Id = guild.Id,
                LogChannel = newLogChannel.Id
            };
            
            await _connection.ExecuteAsync("insert into servers(id, log_channel) values (@Id, @LogChannel) on conflict (id) do update set log_channel = @LogChannel", def);
        }
    }
}