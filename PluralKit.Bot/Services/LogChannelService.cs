using System.Threading.Tasks;

using Dapper;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Types;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot {
    public class LogChannelService {
        private readonly EmbedService _embed;
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly ILogger _logger;
        private readonly IDiscordCache _cache;
        private readonly DiscordApiClient _rest;
        private readonly Bot _bot;

        public LogChannelService(EmbedService embed, ILogger logger, IDatabase db, ModelRepository repo, IDiscordCache cache, DiscordApiClient rest, Bot bot)
        {
            _embed = embed;
            _db = db;
            _repo = repo;
            _cache = cache;
            _rest = rest;
            _bot = bot;
            _logger = logger.ForContext<LogChannelService>();
        }

        public async ValueTask LogMessage(MessageContext ctx, ProxyMatch proxy, Message trigger, ulong hookMessage)
        {
            if (ctx.SystemId == null || ctx.LogChannel == null || ctx.InLogBlacklist) return;
            
            // Find log channel and check if valid
            var logChannel = await FindLogChannel(trigger.GuildId!.Value, ctx.LogChannel.Value);
            if (logChannel == null || logChannel.Type != Channel.ChannelType.GuildText) return;

            var triggerChannel = _cache.GetChannel(trigger.ChannelId);
            
            // Check bot permissions
            var perms = _bot.PermissionsIn(logChannel.Id);
            if (!perms.HasFlag(PermissionSet.SendMessages | PermissionSet.EmbedLinks))
            {
                _logger.Information(
                    "Does not have permission to proxy log, ignoring (channel: {ChannelId}, guild: {GuildId}, bot permissions: {BotPermissions})", 
                    ctx.LogChannel.Value, trigger.GuildId!.Value, perms);
                return;
            }
            
            // Send embed!
            await using var conn = await _db.Obtain();
            var embed = _embed.CreateLoggedMessageEmbed(await _repo.GetSystem(conn, ctx.SystemId.Value), 
                await _repo.GetMember(conn, proxy.Member.Id), hookMessage, trigger.Id, trigger.Author, proxy.Content, 
                triggerChannel);
            var url = $"https://discord.com/channels/{trigger.GuildId}/{trigger.ChannelId}/{hookMessage}";
            await _rest.CreateMessage(logChannel.Id, new() {Content = url, Embed = embed});
        }

        private async Task<Channel?> FindLogChannel(ulong guildId, ulong channelId)
        {
            // TODO: fetch it directly on cache miss?
            if (_cache.TryGetChannel(channelId, out var channel))
                return channel;
            
            // Channel doesn't exist or we don't have permission to access it, let's remove it from the database too
            _logger.Warning("Attempted to fetch missing log channel {LogChannel} for guild {Guild}, removing from database", channelId, guildId);
            await using var conn = await _db.Obtain();
            await conn.ExecuteAsync("update servers set log_channel = null where id = @Guild",
                new {Guild = guildId});

            return null;
        }
    }
}