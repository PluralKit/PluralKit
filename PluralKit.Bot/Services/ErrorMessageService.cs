using System.Diagnostics;

using App.Metrics;

using Myriad.Builders;
using Myriad.Rest;
using Myriad.Rest.Types.Requests;
using Myriad.Types;
using Myriad.Gateway;

using NodaTime;

using Serilog;

namespace PluralKit.Bot;

public class ErrorMessageService
{
    // globally rate limit errors for now, don't want to spam users when something breaks
    private static readonly Duration MinErrorInterval = Duration.FromSeconds(10);
    private static readonly Duration IntervalFromStartup = Duration.FromMinutes(2);

    private readonly ILogger _logger;
    private readonly BotConfig _botConfig;
    private readonly IMetrics _metrics;
    private readonly DiscordApiClient _rest;

    public ErrorMessageService(BotConfig botConfig, IMetrics metrics, ILogger logger, DiscordApiClient rest)
    {
        _botConfig = botConfig;
        _metrics = metrics;
        _logger = logger;
        _rest = rest;

        lastErrorTime = SystemClock.Instance.GetCurrentInstant();
    }

    // private readonly ConcurrentDictionary<ulong, Instant> _lastErrorInChannel = new ConcurrentDictionary<ulong, Instant>();
    private Instant lastErrorTime { get; set; }

    public async Task InteractionRespondWithErrorMessage(InteractionCreateEvent evt, string errorId)
    {
        var now = SystemClock.Instance.GetCurrentInstant();
        if (!ShouldSendErrorMessage(null, now))
        {
            _logger.Warning("Rate limited sending error interaction response for id {InteractionId} with error code {ErrorId}",
                evt.Id, errorId);
            _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "throttled");
            return;
        }

        var embed = CreateErrorEmbed(errorId, now);

        try
        {
            var interactionData = new InteractionApplicationCommandCallbackData
            {
                Content = $"> **Error code:** `{errorId}`",
                Embeds = new[] { embed },
                Flags = Message.MessageFlags.Ephemeral
            };

            await _rest.CreateInteractionResponse(evt.Id, evt.Token,
                new InteractionResponse
                {
                    Type = InteractionResponse.ResponseType.ChannelMessageWithSource,
                    Data = interactionData,
                });

            _logger.Information("Sent error message interaction response for id {InteractionId} with error code {ErrorId}", evt.Id, errorId);
            _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "sent");
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error sending error interaction response for id {InteractionId}", evt.Id);
            _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "failed");
            throw;
        }
    }

    public async Task SendErrorMessage(ulong channelId, string errorId)
    {
        var now = SystemClock.Instance.GetCurrentInstant();
        if (!ShouldSendErrorMessage(channelId, now))
        {
            _logger.Warning("Rate limited sending error message to {ChannelId} with error code {ErrorId}",
                channelId, errorId);
            _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "throttled");
            return;
        }

        var embed = CreateErrorEmbed(errorId, now);

        try
        {
            await _rest.CreateMessage(channelId,
                new MessageRequest { Content = $"> **Error code:** `{errorId}`", Embeds = new[] { embed } });

            _logger.Information("Sent error message to {ChannelId} with error code {ErrorId}", channelId, errorId);
            _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "sent");
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error sending error message to {ChannelId}", channelId);
            _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "failed");
            throw;
        }
    }

    private Embed CreateErrorEmbed(string errorId, Instant now)
    {
        var channelInfo = _botConfig.IsBetaBot
               ? "**#beta-testing** on **[the support server *(click to join)*](https://discord.gg/THvbH59btW)**"
               : "**#bug-reports-and-errors** on **[the support server *(click to join)*](https://discord.gg/PczBt78)**";

        return new EmbedBuilder()
            .Color(0xE74C3C)
            .Title("Internal error occurred")
            .Description($"For support, please send the error code above as text in {channelInfo} with a description of what you were doing at the time.")
            .Footer(new Embed.EmbedFooter(errorId))
            .Timestamp(now.ToDateTimeOffset().ToString("O"))
            .Build();
    }

    private bool ShouldSendErrorMessage(ulong? channelId, Instant now)
    {
        // if (_lastErrorInChannel.TryGetValue(channelId, out var lastErrorTime))

        var startupTime = Instant.FromDateTimeUtc(Process.GetCurrentProcess().StartTime.ToUniversalTime());
        // don't send errors during startup
        // mostly because Npgsql throws a bunch of errors when opening connections sometimes???
        if (now - startupTime < IntervalFromStartup && !_botConfig.IsBetaBot)
            return false;

        var interval = now - lastErrorTime;
        if (interval < MinErrorInterval)
            return false;

        // _lastErrorInChannel[channelId] = now;
        lastErrorTime = now;
        return true;
    }
}