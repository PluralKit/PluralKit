using System.Linq;
using System.Threading.Tasks;

using Dapper;

using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.Exceptions;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot {
    public class LogChannelService {
        private readonly EmbedService _embed;
        private readonly DbConnectionFactory _db;
        private readonly IDataStore _data;
        private readonly ILogger _logger;
        private readonly DiscordRestClient _rest;

        public LogChannelService(EmbedService embed, ILogger logger, DiscordRestClient rest, DbConnectionFactory db, IDataStore data)
        {
            _embed = embed;
            _rest = rest;
            _db = db;
            _data = data;
            _logger = logger.ForContext<LogChannelService>();
        }

        public async Task LogMessage(ProxyMatch proxy, DiscordMessage trigger, ulong hookMessage)
        {
            if (proxy.Member.LogChannel == null || proxy.Member.LogBlacklist.Contains(trigger.ChannelId)) return;
            
            // Find log channel and check if valid
            var logChannel = await FindLogChannel(trigger.Channel.GuildId, proxy);
            if (logChannel == null || logChannel.Type != ChannelType.Text) return;
            
            // Check bot permissions
            if (!trigger.Channel.BotHasAllPermissions(Permissions.SendMessages | Permissions.EmbedLinks)) return;
            
            // Send embed!
            await using var conn = await _db.Obtain();
            var embed = _embed.CreateLoggedMessageEmbed(await _data.GetSystemById(proxy.Member.SystemId),
                await _data.GetMemberById(proxy.Member.MemberId), hookMessage, trigger.Id, trigger.Author, proxy.Content,
                trigger.Channel);
            var url = $"https://discord.com/channels/{trigger.Channel.GuildId}/{trigger.ChannelId}/{hookMessage}";
            await logChannel.SendMessageAsync(content: url, embed: embed);
        }

        private async Task<DiscordChannel> FindLogChannel(ulong guild, ProxyMatch proxy)
        {
            var logChannel = proxy.Member.LogChannel.Value;
            
            try
            {
                return await _rest.GetChannelAsync(logChannel);
            }
            catch (NotFoundException)
            {
                // Channel doesn't exist, let's remove it from the database too
                _logger.Warning("Attempted to fetch missing log channel {LogChannel}, removing from database", logChannel);
                await using var conn = await _db.Obtain();
                await conn.ExecuteAsync("update servers set log_channel = null where server = @Guild",
                    new {Guild = guild});
            }

            return null;
        }
    }
}