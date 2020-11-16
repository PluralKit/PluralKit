using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

using App.Metrics;

using DSharpPlus.Entities;

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
        
        public ErrorMessageService(IMetrics metrics, ILogger logger)
        {
            _metrics = metrics;
            _logger = logger;
        }

        public async Task SendErrorMessage(DiscordChannel channel, string errorId)
        {
            var now = SystemClock.Instance.GetCurrentInstant();
            if (!ShouldSendErrorMessage(channel, now))
            {
                _logger.Warning("Rate limited sending error message to {ChannelId} with error code {ErrorId}", channel.Id, errorId);
                _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "throttled");
                return;
            }

            var embed = new DiscordEmbedBuilder()
                .WithColor(new DiscordColor(0xE74C3C))
                .WithTitle("Internal error occurred")
                .WithDescription("For support, please send the error code above in **#bug-reports-and-errors** on **[the support server *(click to join)*](https://discord.gg/PczBt78)** with a description of what you were doing at the time.")
                .WithFooter(errorId)
                .WithTimestamp(now.ToDateTimeOffset());

            try
            {
                await channel.SendMessageAsync($"> **Error code:** `{errorId}`", embed: embed.Build());
                _logger.Information("Sent error message to {ChannelId} with error code {ErrorId}", channel.Id, errorId);
                _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "sent");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error sending error message to {ChannelId}", channel.Id);
                _metrics.Measure.Meter.Mark(BotMetrics.ErrorMessagesSent, "failed");
                throw;
            }
        }

        private bool ShouldSendErrorMessage(DiscordChannel channel, Instant now)
        {
            if (_lastErrorInChannel.TryGetValue(channel.Id, out var lastErrorTime))
            {
                var interval = now - lastErrorTime;
                if (interval < MinErrorInterval)
                    return false;
            }

            _lastErrorInChannel[channel.Id] = now;
            return true;
        }
    }
}