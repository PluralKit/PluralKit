using System.Threading.Tasks;
using Dapper;
using Discord;
using Serilog;

namespace PluralKit.Bot {
    public class LogChannelService {
        private IDiscordClient _client;
        private EmbedService _embed;
        private IDataStore _data;
        private ILogger _logger;

        public LogChannelService(IDiscordClient client, EmbedService embed, ILogger logger, IDataStore data)
        {
            _client = client;
            _embed = embed;
            _data = data;
            _logger = logger.ForContext<LogChannelService>();
        }

        public async Task LogMessage(PKSystem system, PKMember member, ulong messageId, ulong originalMsgId, IGuildChannel originalChannel, IUser sender, string content) {
            var logChannel = await GetLogChannel(originalChannel.Guild);
            if (logChannel == null) return;

            var embed = _embed.CreateLoggedMessageEmbed(system, member, messageId, originalMsgId, sender, content, originalChannel);

            var url = $"https://discordapp.com/channels/{originalChannel.GuildId}/{originalChannel.Id}/{messageId}";
            await logChannel.SendMessageAsync(text: url, embed: embed);
        }

        private async Task<ITextChannel> GetLogChannel(IGuild guild)
        {
            var guildCfg = await _data.GetGuildConfig(guild.Id);
            if (guildCfg.LogChannel == null) return null;
            return await _client.GetChannelAsync(guildCfg.LogChannel.Value) as ITextChannel;
        }
    }
}