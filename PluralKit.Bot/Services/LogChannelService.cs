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
    private readonly BotConfig _config;
    private readonly IDiscordCache _cache;
    private readonly IDatabase _db;
    private readonly EmbedService _embed;
    private readonly ILogger _logger;
    private readonly ModelRepository _repo;
    private readonly DiscordApiClient _rest;

    public LogChannelService(EmbedService embed, ILogger logger, IDatabase db, ModelRepository repo,
                             IDiscordCache cache, DiscordApiClient rest, Bot bot, BotConfig config)
    {
        _embed = embed;
        _db = db;
        _repo = repo;
        _cache = cache;
        _rest = rest;
        _bot = bot;
        _config = config;
        _logger = logger.ForContext<LogChannelService>();
    }

    public async ValueTask LogMessage(PKMessage proxiedMessage, Message trigger, Message hookMessage, string oldContent = null)
    {
        var logChannelId = await GetAndCheckLogChannel(trigger, proxiedMessage);
        if (logChannelId == null)
            return;

        var triggerChannel = await _cache.GetChannel(proxiedMessage.Guild!.Value, proxiedMessage.Channel);

        var member = await _repo.GetMember(proxiedMessage.Member!.Value);
        var system = await _repo.GetSystem(member.System);

        // Send embed!
        var embed = _embed.CreateLoggedMessageEmbed(trigger, hookMessage, system.Hid, member, triggerChannel.Name,
            oldContent);
        var url =
            $"https://discord.com/channels/{proxiedMessage.Guild.Value}/{proxiedMessage.Channel}/{proxiedMessage.Mid}";
        await _rest.CreateMessage(logChannelId.Value, new MessageRequest { Content = url, Embeds = new[] { embed } });
    }

    private async Task<ulong?> GetAndCheckLogChannel(Message trigger, PKMessage proxiedMessage)
    {
        if (proxiedMessage.Guild == null && proxiedMessage.Channel != trigger.ChannelId)
            // a very old message is being edited outside of its original channel
            // we can't know if we're in the correct guild, so skip fetching a log channel
            return null;

        var guildId = proxiedMessage.Guild ?? trigger.GuildId.Value;
        var rootChannel = await _cache.GetRootChannel(guildId, proxiedMessage.Channel);

        // get log channel info from the database
        var guild = await _repo.GetGuild(guildId);
        var logChannelId = guild.LogChannel;
        var isBlacklisted = guild.LogBlacklist.Any(x => x == proxiedMessage.Channel || x == rootChannel.Id);

        // if (ctx.SystemId == null ||
        // removed the above, there shouldn't be a way to get to this code path if you don't have a system registered
        if (logChannelId == null || isBlacklisted) return null;

        // Find log channel and check if valid
        var logChannel = await FindLogChannel(guildId, logChannelId.Value);
        if (logChannel == null || logChannel.Type != Channel.ChannelType.GuildText && logChannel.Type != Channel.ChannelType.GuildPublicThread && logChannel.Type != Channel.ChannelType.GuildPrivateThread) return null;

        // Check bot permissions
        var perms = await GetPermissionsInLogChannel(logChannel);
        if (!perms.HasFlag(PermissionSet.SendMessages | PermissionSet.EmbedLinks))
        {
            _logger.Information(
                "Does not have permission to log proxy, ignoring (channel: {ChannelId}, guild: {GuildId}, bot permissions: {BotPermissions})",
                logChannel.Id, guildId, perms);
            return null;
        }

        return logChannel.Id;
    }

    // todo: move this somewhere else
    private async Task<PermissionSet> GetPermissionsInLogChannel(Channel channel)
    {
        var guild = await _cache.TryGetGuild(channel.GuildId.Value);
        if (guild == null)
            guild = await _rest.GetGuild(channel.GuildId.Value);

        var guildMember = await _cache.TryGetSelfMember(channel.GuildId.Value);
        if (guildMember == null)
            guildMember = await _rest.GetGuildMember(channel.GuildId.Value, _config.ClientId);

        var perms = PermissionExtensions.PermissionsFor(guild, channel, _config.ClientId, guildMember);
        return perms;
    }

    private async Task<Channel?> FindLogChannel(ulong guildId, ulong channelId)
    {
        // TODO: fetch it directly on cache miss?
        if (await _cache.TryGetChannel(guildId, channelId) is Channel channel)
            return channel;

        if (await _rest.GetChannelOrNull(channelId) is Channel restChannel)
            return restChannel;

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