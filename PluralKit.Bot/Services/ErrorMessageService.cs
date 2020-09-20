using System.Collections.Concurrent;
using System.Threading.Tasks;

using DSharpPlus.Entities;

using NodaTime;

namespace PluralKit.Bot
{
    public class ErrorMessageService
    {
        private static readonly Duration MinErrorInterval = Duration.FromSeconds(10);
        private readonly ConcurrentDictionary<ulong, Instant> _lastErrorInChannel = new ConcurrentDictionary<ulong, Instant>();

        public async Task SendErrorMessage(DiscordChannel channel, string errorId)
        {
            var now = SystemClock.Instance.GetCurrentInstant(); 
            if (_lastErrorInChannel.TryGetValue(channel.Id, out var lastErrorTime))
            {
                var interval = now - lastErrorTime;
                if (interval < MinErrorInterval)
                    return;
            }
            _lastErrorInChannel[channel.Id] = now;
            
            var embed = new DiscordEmbedBuilder()
                .WithColor(new DiscordColor(0xE74C3C))
                .WithTitle("Internal error occurred")
                .WithDescription("For support, please send the error code above in **#bug-reports-and-errors** on **[the support server *(click to join)*](https://discord.gg/PczBt78)** with a description of what you were doing at the time.")
                .WithFooter(errorId)
                .WithTimestamp(now.ToDateTimeOffset());
            await channel.SendMessageAsync($"> **Error code:** `{errorId}`", embed: embed.Build());
        }
    }
}