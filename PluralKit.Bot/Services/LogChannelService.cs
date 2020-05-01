using System.Threading.Tasks;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot {
    public class LogChannelService {
        private EmbedService _embed;
        private IDataStore _data;
        private ILogger _logger;

        public LogChannelService(EmbedService embed, ILogger logger, IDataStore data)
        {
            _embed = embed;
            _data = data;
            _logger = logger.ForContext<LogChannelService>();
        }

        public async Task LogMessage(DiscordClient client, PKSystem system, PKMember member, ulong messageId, ulong originalMsgId, DiscordChannel originalChannel, DiscordUser sender, string content, GuildConfig? guildCfg = null)
        {
            if (guildCfg == null) 
                guildCfg = await _data.GetOrCreateGuildConfig(originalChannel.GuildId);

            // Bail if logging is disabled either globally or for this channel
            if (guildCfg.Value.LogChannel == null) return;
            if (guildCfg.Value.LogBlacklist.Contains(originalChannel.Id)) return;
            
            // Bail if we can't find the channel
            DiscordChannel channel;
            try
            {
                channel = await client.GetChannelAsync(guildCfg.Value.LogChannel.Value);
            }
            catch (NotFoundException)
            {
                // If it doesn't exist, remove it from the DB
                await RemoveLogChannel(guildCfg.Value);
                return; 
            }
            
            // Bail if it's not a text channel
            if (channel.Type != ChannelType.Text) return;

            // Bail if we don't have permission to send stuff here
            var neededPermissions = Permissions.SendMessages | Permissions.EmbedLinks;
            if ((channel.BotPermissions() & neededPermissions) != neededPermissions)
                return;

            var embed = _embed.CreateLoggedMessageEmbed(system, member, messageId, originalMsgId, sender, content, originalChannel);

            var url = $"https://discordapp.com/channels/{originalChannel.GuildId}/{originalChannel.Id}/{messageId}";
            
            await channel.SendMessageAsync(content: url, embed: embed);
        }

        private async Task RemoveLogChannel(GuildConfig cfg)
        {
            cfg.LogChannel = null;
            await _data.SaveGuildConfig(cfg);
        }
    }
}