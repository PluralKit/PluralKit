using System.Linq;
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

        public async ValueTask LogMessage(MessageContext ctx, PKMessage message, Message trigger, Message hookMessage, string oldContent = null)
        {
            Channel? logChannel;
            if (oldContent == null)
                logChannel = await GetAndCheckLogChannel(ctx, trigger);
            else
                logChannel = await GetAndCheckLogChannel(ctx, trigger, message);

            if (logChannel == null) return;

            var triggerChannel = _cache.GetChannel(message.Channel);
            
            await using var conn = await _db.Obtain();
            var system = await _repo.GetSystem(conn, ctx.SystemId.Value);
            var member = await _repo.GetMember(conn, message.Member);

            // Send embed!
            var embed = _embed.CreateLoggedMessageEmbed(trigger, hookMessage, system.Hid, member.Hid, triggerChannel.Name, oldContent);
            var url = $"https://discord.com/channels/{message.Guild.Value}/{message.Channel}/{message.Mid}";
            await _rest.CreateMessage(logChannel.Id, new() {Content = url, Embed = embed});
        }

        private async Task<Channel?> GetAndCheckLogChannel(MessageContext ctx, Message trigger, PKMessage original = null)
        {
            var guildId = trigger.GuildId != null ? trigger.GuildId!.Value : original.Guild.Value;
            var logChannelId = ctx.LogChannel;
            var isBlacklisted = ctx.InLogBlacklist;

            if (original != null)
            {
                // we're editing a message, get log channel info from the database
                var guild = await _db.Execute(c => _repo.GetGuild(c, original.Guild.Value));
                logChannelId = guild.LogChannel;
                isBlacklisted = guild.Blacklist.Any(x => x == logChannelId);
            }

            if (ctx.SystemId == null || logChannelId == null || isBlacklisted) return null;
            

            // Find log channel and check if valid
            var logChannel = await FindLogChannel(guildId, logChannelId.Value);
            if (logChannel == null || logChannel.Type != Channel.ChannelType.GuildText) return null;
            
            // Check bot permissions
            var perms = _bot.PermissionsIn(logChannel.Id);
            if (!perms.HasFlag(PermissionSet.SendMessages | PermissionSet.EmbedLinks))
            {
                _logger.Information(
                    "Does not have permission to proxy log, ignoring (channel: {ChannelId}, guild: {GuildId}, bot permissions: {BotPermissions})", 
                    ctx.LogChannel.Value, trigger.GuildId!.Value, perms);
                return null;
            }

            return logChannel;
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