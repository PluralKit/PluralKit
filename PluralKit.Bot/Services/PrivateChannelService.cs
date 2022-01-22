using Serilog;

using Myriad.Cache;
using Myriad.Gateway;
using Myriad.Rest;

using PluralKit.Core;

namespace PluralKit.Bot;

public class PrivateChannelService
{
    private readonly ILogger _logger;
    private readonly ModelRepository _repo;
    private readonly DiscordApiClient _rest;

    private static Dictionary<ulong, ulong> _channelsCache = new();
    public PrivateChannelService(ILogger logger, ModelRepository repo, DiscordApiClient rest)
    {
        _logger = logger;
        _repo = repo;
        _rest = rest;
    }

    public async Task TrySavePrivateChannel(MessageCreateEvent evt)
    {
        if (evt.GuildId != null) return;
        if (_channelsCache.TryGetValue(evt.Author.Id, out _)) return;

        await SaveDmChannel(evt.Author.Id, evt.ChannelId);
    }

    public async Task<ulong> GetOrCreateDmChannel(ulong userId)
    {
        if (_channelsCache.TryGetValue(userId, out var cachedChannelId))
            return cachedChannelId;

        var channelId = await _repo.GetDmChannel(userId);
        if (channelId == null)
        {
            var channel = await _rest.CreateDm(userId);
            channelId = channel.Id;
        }

        // spawn off saving the channel as to not block the current thread
        _ = SaveDmChannel(userId, channelId.Value);

        return channelId.Value;
    }

    private async Task SaveDmChannel(ulong userId, ulong channelId)
    {
        try
        {
            _channelsCache.Add(userId, channelId);
            await _repo.UpdateAccount(userId, new() { DmChannel = channelId });
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to save DM channel {ChannelId} for user {UserId}", channelId, userId);
        }
    }
}