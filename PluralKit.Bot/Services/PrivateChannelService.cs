using App.Metrics;

using Serilog;

using Myriad.Gateway;
using Myriad.Rest;

using PluralKit.Core;

namespace PluralKit.Bot;

public class PrivateChannelService
{
    private readonly IMetrics _metrics;
    private readonly ILogger _logger;
    private readonly ModelRepository _repo;
    private readonly DiscordApiClient _rest;
    public PrivateChannelService(IMetrics metrics, ILogger logger, ModelRepository repo, DiscordApiClient rest)
    {
        _metrics = metrics;
        _logger = logger;
        _repo = repo;
        _rest = rest;
    }

    public async Task TrySavePrivateChannel(MessageCreateEvent evt)
    {
        if (evt.GuildId == null) await SaveDmChannel(evt.Author.Id, evt.ChannelId);
    }

    public async Task<ulong> GetOrCreateDmChannel(ulong userId)
    {
        var channelId = await _repo.GetDmChannel(userId);
        if (channelId != null)
        {
            _metrics.Measure.Meter.Mark(BotMetrics.DatabaseDMCacheHits);
            return channelId.Value;
        }

        _metrics.Measure.Meter.Mark(BotMetrics.DMCacheMisses);

        var channel = await _rest.CreateDm(userId);

        // spawn off saving the channel as to not block the current thread
        _ = SaveDmChannel(userId, channel.Id);

        return channel.Id;
    }

    private async Task SaveDmChannel(ulong userId, ulong channelId)
    {
        try
        {
            await _repo.UpdateAccount(userId, new() { DmChannel = channelId });
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to save DM channel {ChannelId} for user {UserId}", channelId, userId);
        }
    }
}