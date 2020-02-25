using System.Threading.Tasks;

using Discord;

using PluralKit.Core;

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

        public async Task LogMessage(PKSystem system, PKMember member, ulong messageId, ulong originalMsgId, IGuildChannel originalChannel, IUser sender, string content, GuildConfig? guildCfg = null)
        {
            if (guildCfg == null) 
                guildCfg = await _data.GetOrCreateGuildConfig(originalChannel.GuildId);

            // Bail if logging is disabled either globally or for this channel
            if (guildCfg.Value.LogChannel == null) return;
            if (guildCfg.Value.LogBlacklist.Contains(originalChannel.Id)) return;
            
            // Bail if we can't find the channel
            if (!(await _client.GetChannelAsync(guildCfg.Value.LogChannel.Value) is ITextChannel logChannel)) return;

            // Bail if we don't have permission to send stuff here
            if (!logChannel.HasPermission(ChannelPermission.SendMessages) || !logChannel.HasPermission(ChannelPermission.EmbedLinks))
                return;

            var embed = _embed.CreateLoggedMessageEmbed(system, member, messageId, originalMsgId, sender, content, originalChannel);

            var url = $"https://discordapp.com/channels/{originalChannel.GuildId}/{originalChannel.Id}/{messageId}";
            
            await logChannel.SendMessageAsync(text: url, embed: embed);
        }
    }
}