using System;
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
        private readonly IDatabase _db;
        private readonly IDataStore _data;
        private readonly ILogger _logger;
        private readonly DiscordRestClient _rest;

        public LogChannelService(EmbedService embed, ILogger logger, DiscordRestClient rest, IDatabase db, IDataStore data)
        {
            _embed = embed;
            _rest = rest;
            _db = db;
            _data = data;
            _logger = logger.ForContext<LogChannelService>();
        }

        public async ValueTask LogMessage(MessageContext ctx, ProxyMatch proxy, DiscordMessage trigger, ulong hookMessage)
        {
            if (ctx.SystemId == null || ctx.LogChannel == null || ctx.InLogBlacklist) return;
            
            // Find log channel and check if valid
            var logChannel = await FindLogChannel(trigger.Channel.GuildId, ctx.LogChannel.Value);
            if (logChannel == null || logChannel.Type != ChannelType.Text) return;
            
            // Check bot permissions
            if (!trigger.Channel.BotHasAllPermissions(Permissions.SendMessages | Permissions.EmbedLinks)) return;
            
            // Send embed!
            await using var conn = await _db.Obtain();
            var embed = _embed.CreateLoggedMessageEmbed(await conn.QuerySystem(ctx.SystemId.Value),
                await conn.QueryMember(proxy.Member.Id), hookMessage, trigger.Id, trigger.Author, proxy.Content,
                trigger.Channel);
            var url = $"https://discord.com/channels/{trigger.Channel.GuildId}/{trigger.ChannelId}/{hookMessage}";
            await logChannel.SendMessageFixedAsync(content: url, embed: embed);
        }

        private async Task<DiscordChannel> FindLogChannel(ulong guild, ulong channel)
        {
            try
            {
                return await _rest.GetChannelAsync(channel);
            }
            catch (Exception e) when (e is NotFoundException || e is UnauthorizedException)  
            {
                // Channel doesn't exist or we don't have permission to access it, let's remove it from the database too
                _logger.Warning("Attempted to fetch missing log channel {LogChannel}, removing from database", channel);
                await using var conn = await _db.Obtain();
                await conn.ExecuteAsync("update servers set log_channel = null where server = @Guild",
                    new {Guild = guild});
            }

            return null;
        }
    }
}