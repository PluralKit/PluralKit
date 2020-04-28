using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using App.Metrics;

using DSharpPlus;
using DSharpPlus.Entities;

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
        private WebhookRateLimitService _webhookRateLimitCache;

        private DbConnectionCountHolder _countHolder;

        private ILogger _logger;

        public PeriodicStatCollector(DiscordShardedClient client, IMetrics metrics, ILogger logger, WebhookCacheService webhookCache, DbConnectionCountHolder countHolder, IDataStore data, CpuStatService cpu, WebhookRateLimitService webhookRateLimitCache)
        {
            _client = client;
            _metrics = metrics;
            _webhookCache = webhookCache;
            _countHolder = countHolder;
            _data = data;
            _cpu = cpu;
            _webhookRateLimitCache = webhookRateLimitCache;
            _logger = logger.ForContext<PeriodicStatCollector>();
        }

        public async Task CollectStats()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            
            // Aggregate guild/channel stats

            var guildCount = 0;
            var channelCount = 0;
            // No LINQ today, sorry
            foreach (var shard in _client.ShardClients.Values)
            {
                guildCount += shard.Guilds.Count;
                foreach (var guild in shard.Guilds.Values)
                foreach (var channel in guild.Channels.Values)
                    if (channel.Type == ChannelType.Text)
                        channelCount++;
            }
            
            _metrics.Measure.Gauge.SetValue(BotMetrics.Guilds, guildCount);
            _metrics.Measure.Gauge.SetValue(BotMetrics.Channels, channelCount);

            // Aggregate member stats
            var usersKnown = new HashSet<ulong>();
            var usersOnline = new HashSet<ulong>();
            foreach (var shard in _client.ShardClients.Values)
            foreach (var guild in shard.Guilds.Values)
            foreach (var user in guild.Members.Values)
            {
                usersKnown.Add(user.Id);
                
                // Presence updates are disabled, for now we just assume every user is online, I guess
                usersOnline.Add(user.Id);
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
            _metrics.Measure.Gauge.SetValue(BotMetrics.WebhookRateLimitCacheSize, _webhookRateLimitCache.CacheSize);

            stopwatch.Stop();
            _logger.Information("Updated metrics in {Time}", stopwatch.ElapsedDuration());
        }
    }
}