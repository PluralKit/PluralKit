using System.Threading.Tasks;
using Dapper;
using Discord;
using Serilog;

namespace PluralKit.Bot {
    public class ServerDefinition {
        public ulong Id { get; set; }
        public ulong? LogChannel { get; set; }
    }

    public class LogChannelService {
        private IDiscordClient _client;
        private DbConnectionFactory _conn;
        private EmbedService _embed;
        private ILogger _logger;

        public LogChannelService(IDiscordClient client, DbConnectionFactory conn, EmbedService embed, ILogger logger)
        {
            this._client = client;
            this._conn = conn;
            this._embed = embed;
            _logger = logger.ForContext<LogChannelService>();
        }

        public async Task LogMessage(PKSystem system, PKMember member, ulong messageId, ulong originalMsgId, IGuildChannel originalChannel, IUser sender, string content) {
            var logChannel = await GetLogChannel(originalChannel.Guild);
            if (logChannel == null) return;

            var embed = _embed.CreateLoggedMessageEmbed(system, member, messageId, originalMsgId, sender, content, originalChannel);

            var url = $"https://discordapp.com/channels/{originalChannel.GuildId}/{originalChannel.Id}/{messageId}";
            await logChannel.SendMessageAsync(text: url, embed: embed);
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

            _logger.Information("Set guild {Guild} log channel to {Channel}", guild.Id, newLogChannel?.Id);
        }
    }
}