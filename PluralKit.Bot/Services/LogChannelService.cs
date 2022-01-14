using Dapper;

using Myriad.Cache;
using Myriad.Extensions;
using Myriad.Rest;
using Myriad.Rest.Types.Requests;
using Myriad.Types;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot;

public class LogChannelService
{
    private readonly Bot _bot;
    private readonly IDiscordCache _cache;
    private readonly IDatabase _db;
    private readonly EmbedService _embed;
    private readonly ILogger _logger;
    private readonly ModelRepository _repo;
    private readonly DiscordApiClient _rest;

    public LogChannelService(EmbedService embed, ILogger logger, IDatabase db, ModelRepository repo,
                             IDiscordCache cache, DiscordApiClient rest, Bot bot)
    {
        _embed = embed;
        _db = db;
        _repo = repo;
        _cache = cache;
        _rest = rest;
        _bot = bot;
        _logger = logger.ForContext<LogChannelService>();
    }

    public async ValueTask LogMessage(MessageContext ctx, PKMessage proxiedMessage, Message trigger,
                                      Message hookMessage, string oldContent = null)
    {
        var logChannelId = await GetAndCheckLogChannel(ctx, trigger, proxiedMessage);
        if (logChannelId == null)
            return;

        var triggerChannel = await _cache.GetChannel(proxiedMessage.Channel);

        var system = await _repo.GetSystem(ctx.SystemId.Value);
        var member = await _repo.GetMember(proxiedMessage.Member!.Value);

        // Send embed!
        var embed = _embed.CreateLoggedMessageEmbed(trigger, hookMessage, system.Hid, member, triggerChannel.Name,
            oldContent);
        var url =
            $"https://discord.com/channels/{proxiedMessage.Guild.Value}/{proxiedMessage.Channel}/{proxiedMessage.Mid}";
        await _rest.CreateMessage(logChannelId.Value, new MessageRequest { Content = url, Embed = embed });
    }

    private async Task<ulong?> GetAndCheckLogChannel(MessageContext ctx, Message trigger,
                                                       PKMessage proxiedMessage)
    {
        if (proxiedMessage.Guild == null && proxiedMessage.Channel != trigger.ChannelId)
            // a very old message is being edited outside of its original channel
            // we can't know if we're in the correct guild, so skip fetching a log channel
            return null;

        var guildId = proxiedMessage.Guild ?? trigger.GuildId.Value;
        var logChannelId = ctx.LogChannel;
        var isBlacklisted = ctx.InLogBlacklist;

        if (proxiedMessage.Guild != trigger.GuildId)
        {
            // we're editing a message from a different server, get log channel info from the database
            var guild = await _repo.GetGuild(proxiedMessage.Guild.Value);
            logChannelId = guild.LogChannel;
            isBlacklisted = guild.LogBlacklist.Any(x => x == trigger.ChannelId);
        }

        if (ctx.SystemId == null || logChannelId == null || isBlacklisted) return null;

        // Find log channel and check if valid
        var logChannel = await FindLogChannel(guildId, logChannelId.Value);
        if (logChannel == null || logChannel.Type != Channel.ChannelType.GuildText) return null;

        // Check bot permissions
        var perms = await _cache.PermissionsIn(logChannel.Id);
        if (!perms.HasFlag(PermissionSet.SendMessages | PermissionSet.EmbedLinks))
        {
            _logger.Information(
                "Does not have permission to log proxy, ignoring (channel: {ChannelId}, guild: {GuildId}, bot permissions: {BotPermissions})",
                ctx.LogChannel.Value, trigger.GuildId!.Value, perms);
            return null;
        }

        return logChannel.Id;
    }

    private async Task<Channel?> FindLogChannel(ulong guildId, ulong channelId)
    {
        // TODO: fetch it directly on cache miss?
        if (await _cache.TryGetChannel(channelId) is Channel channel)
            return channel;

        // Channel doesn't exist or we don't have permission to access it, let's remove it from the database too
        _logger.Warning(
            "Attempted to fetch missing log channel {LogChannel} for guild {Guild}, removing from database",
            channelId, guildId
        );
        await using var conn = await _db.Obtain();
        await conn.ExecuteAsync("update servers set log_channel = null where id = @Guild",
            new { Guild = guildId });

        return null;
    }
}