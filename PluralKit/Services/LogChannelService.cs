using System.Data;
using System.Threading.Tasks;
using Dapper;
using Discord;

namespace PluralKit {
    class ServerDefinition {
        public ulong Id;
        public ulong LogChannel;
    }

    class LogChannelService {
        private IDiscordClient _client;
        private IDbConnection _connection;

        public LogChannelService(IDiscordClient client, IDbConnection connection)
        {
            this._client = client;
            this._connection = connection;
        }

        public async Task LogMessage(PKSystem system, PKMember member, IMessage message, IUser sender) {
            var channel = await GetLogChannel((message.Channel as IGuildChannel).Guild);
            if (channel == null) return;

            var embed = new EmbedBuilder()
                .WithAuthor($"#{message.Channel.Name}: {member.Name}", member.AvatarUrl)
                .WithDescription(message.Content)
                .WithFooter($"System ID: {system.Hid} | Member ID: {member.Hid} | Sender: ${sender.Username}#{sender.Discriminator} ({sender.Id}) | Message ID: ${message.Id}")
                .WithTimestamp(message.Timestamp)
                .Build();
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