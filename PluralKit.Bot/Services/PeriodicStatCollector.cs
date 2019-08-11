using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using Discord;
using Discord.WebSocket;
using NodaTime.Extensions;
using PluralKit.Core;
using Serilog;

namespace PluralKit.Bot
{
    public class PeriodicStatCollector
    {
        private DiscordShardedClient _client;
        private IMetrics _metrics;

        private SystemStore _systems;
        private MemberStore _members;
        private SwitchStore _switches;
        private MessageStore _messages;

        private WebhookCacheService _webhookCache;

        private DbConnectionCountHolder _countHolder;

        private ILogger _logger;

        public PeriodicStatCollector(IDiscordClient client, IMetrics metrics, SystemStore systems, MemberStore members, SwitchStore switches, MessageStore messages, ILogger logger, WebhookCacheService webhookCache, DbConnectionCountHolder countHolder)
        {
            _client = (DiscordShardedClient) client;
            _metrics = metrics;
            _systems = systems;
            _members = members;
            _switches = switches;
            _messages = messages;
            _webhookCache = webhookCache;
            _countHolder = countHolder;
            _logger = logger.ForContext<PeriodicStatCollector>();
        }

        public async Task CollectStats()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            // Aggregate guild/channel stats
            _metrics.Measure.Gauge.SetValue(BotMetrics.Guilds, _client.Guilds.Count);
            _metrics.Measure.Gauge.SetValue(BotMetrics.Channels, _client.Guilds.Sum(g => g.TextChannels.Count));

            // Aggregate member stats
            var usersKnown = new HashSet<ulong>();
            var usersOnline = new HashSet<ulong>();
            foreach (var guild in _client.Guilds)
            foreach (var user in guild.Users)
            {
                usersKnown.Add(user.Id);
                if (user.Status == UserStatus.Online) usersOnline.Add(user.Id);
            }

            _metrics.Measure.Gauge.SetValue(BotMetrics.MembersTotal, usersKnown.Count);
            _metrics.Measure.Gauge.SetValue(BotMetrics.MembersOnline, usersOnline.Count);
            
            // Aggregate DB stats
            _metrics.Measure.Gauge.SetValue(CoreMetrics.SystemCount, await _systems.Count());
            _metrics.Measure.Gauge.SetValue(CoreMetrics.MemberCount, await _members.Count());
            _metrics.Measure.Gauge.SetValue(CoreMetrics.SwitchCount, await _switches.Count());
            _metrics.Measure.Gauge.SetValue(CoreMetrics.MessageCount, await _messages.Count());
            
            // Process info
            var process = Process.GetCurrentProcess();
            _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessPhysicalMemory, process.WorkingSet64);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessVirtualMemory, process.VirtualMemorySize64);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessPrivateMemory, process.PrivateMemorySize64);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessThreads, process.Threads.Count);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessHandles, process.HandleCount);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.CpuUsage, await EstimateCpuUsage());
            
            // Database info
            _metrics.Measure.Gauge.SetValue(CoreMetrics.DatabaseConnections, _countHolder.ConnectionCount);
            
            // Other shiz
            _metrics.Measure.Gauge.SetValue(BotMetrics.WebhookCacheSize, _webhookCache.CacheSize);

            stopwatch.Stop();
            _logger.Information("Updated metrics in {Time}", stopwatch.ElapsedDuration());
        }

        private async Task<double> EstimateCpuUsage()
        {
            // We get the current processor time, wait 5 seconds, then compare
            // https://medium.com/@jackwild/getting-cpu-usage-in-net-core-7ef825831b8b
            
            _logger.Information("Estimating CPU usage...");
            var stopwatch = new Stopwatch();
            
            stopwatch.Start();
            var cpuTimeBefore = Process.GetCurrentProcess().TotalProcessorTime;
            
            await Task.Delay(5000);
            
            stopwatch.Stop();
            var cpuTimeAfter = Process.GetCurrentProcess().TotalProcessorTime;

            var cpuTimePassed = cpuTimeAfter - cpuTimeBefore;
            var timePassed = stopwatch.Elapsed;

            var percent = cpuTimePassed / timePassed;
            _logger.Information("CPU usage measured as {Percent:P}", percent);
            return percent;
        }
    }
}