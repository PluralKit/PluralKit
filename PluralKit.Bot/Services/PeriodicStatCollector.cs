using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        private CpuStatService _cpu;

        private IDataStore _data;

        private WebhookCacheService _webhookCache;

        private DbConnectionCountHolder _countHolder;

        private ILogger _logger;

        public PeriodicStatCollector(IDiscordClient client, IMetrics metrics, ILogger logger, WebhookCacheService webhookCache, DbConnectionCountHolder countHolder, IDataStore data, CpuStatService cpu)
        {
            _client = (DiscordShardedClient) client;
            _metrics = metrics;
            _webhookCache = webhookCache;
            _countHolder = countHolder;
            _data = data;
            _cpu = cpu;
            _logger = logger.ForContext<PeriodicStatCollector>();
        }

        public async Task CollectStats()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            // Aggregate guild/channel stats
            _metrics.Measure.Gauge.SetValue(BotMetrics.Guilds, _client.Guilds.Count);
            _metrics.Measure.Gauge.SetValue(BotMetrics.Channels, _client.Guilds.Sum(g => g.TextChannels.Count));
            _metrics.Measure.Gauge.SetValue(BotMetrics.ShardsConnected, _client.Shards.Count(shard => shard.ConnectionState == ConnectionState.Connected));

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
            _metrics.Measure.Gauge.SetValue(CoreMetrics.SystemCount, await _data.GetTotalSystems());
            _metrics.Measure.Gauge.SetValue(CoreMetrics.MemberCount, await _data.GetTotalMembers());
            _metrics.Measure.Gauge.SetValue(CoreMetrics.SwitchCount, await _data.GetTotalSwitches());
            _metrics.Measure.Gauge.SetValue(CoreMetrics.MessageCount, await _data.GetTotalMessages());
            
            // Process info
            var process = Process.GetCurrentProcess();
            _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessPhysicalMemory, process.WorkingSet64);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessVirtualMemory, process.VirtualMemorySize64);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessPrivateMemory, process.PrivateMemorySize64);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessThreads, process.Threads.Count);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessHandles, process.HandleCount);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.CpuUsage, await _cpu.EstimateCpuUsage());
            
            // Database info
            _metrics.Measure.Gauge.SetValue(CoreMetrics.DatabaseConnections, _countHolder.ConnectionCount);
            
            // Other shiz
            _metrics.Measure.Gauge.SetValue(BotMetrics.WebhookCacheSize, _webhookCache.CacheSize);

            stopwatch.Stop();
            _logger.Information("Updated metrics in {Time}", stopwatch.ElapsedDuration());
        }
    }
}