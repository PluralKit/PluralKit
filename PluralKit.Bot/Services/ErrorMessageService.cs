using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using App.Metrics;

using Myriad.Builders;
using Myriad.Rest;

using NodaTime;

using Serilog;

namespace PluralKit.Bot
{
    public class ErrorMessageService
    {
        private static readonly Duration MinErrorInterval = Duration.FromSeconds(10);
        private readonly ConcurrentDictionary<ulong, Instant> _lastErrorInChannel = new ConcurrentDictionary<ulong, Instant>();
        
        private readonly IMetrics _metrics;
        private readonly ILogger _logger;
        private readonly DiscordApiClient _rest;
        
        public ErrorMessageService(IMetrics metrics, ILogger logger, DiscordApiClient rest)
        {
            _metrics = metrics;
            _logger = logger;
            _rest = rest;
        }

        public async Task SendErrorMessage(ulong channelId, string errorId)
        {
            var now = SystemClock.Instance.GetCurrentInstant();
            if (!ShouldSendErrorMessage(channelId, now))
            {
                _logger.Warning("Rate limited sending error message to {ChannelId} with error code {ErrorId}", channelId, errorId);
                _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "throttled");
                return;
            }

            var embed = new EmbedBuilder()
                .Color(0xE74C3C)
                .Title("Internal error occurred")
                .Description("For support, please send the error code above in **#bug-reports-and-errors** on **[the support server *(click to join)*](https://discord.gg/PczBt78)** with a description of what you were doing at the time.")
                .Footer(new(errorId))
                .Timestamp(now.ToDateTimeOffset().ToString("O"));

            try
            {
                await _rest.CreateMessage(channelId, new()
                {
                    Content = $"> **Error code:** `{errorId}`",
                    Embed = embed.Build()
                });
                
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

        private bool ShouldSendErrorMessage(ulong channelId, Instant now)
        {
            if (_lastErrorInChannel.TryGetValue(channelId, out var lastErrorTime))
            {
                var interval = now - lastErrorTime;
                if (interval < MinErrorInterval)
                    return false;
            }

            _lastErrorInChannel[channelId] = now;
            return true;
        }
    }
}