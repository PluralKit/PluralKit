using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using App.Metrics;

using Dapper;

using DSharpPlus;
using DSharpPlus.Entities;

using NodaTime.Extensions;
using PluralKit.Core;

using Serilog;

namespace PluralKit.Bot
{
    public class PeriodicStatCollector
    {
        private readonly DiscordShardedClient _client;
        private readonly IMetrics _metrics;
        private readonly CpuStatService _cpu;

        private readonly IDatabase _db;

        private readonly WebhookCacheService _webhookCache;

        private readonly DbConnectionCountHolder _countHolder;

        private readonly ILogger _logger;

        public PeriodicStatCollector(DiscordShardedClient client, IMetrics metrics, ILogger logger, WebhookCacheService webhookCache, DbConnectionCountHolder countHolder, CpuStatService cpu, IDatabase db)
        {
            _client = client;
            _metrics = metrics;
            _webhookCache = webhookCache;
            _countHolder = countHolder;
            _cpu = cpu;
            _db = db;
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
                if (user.Presence?.Status == UserStatus.Online)
                    usersOnline.Add(user.Id);
            }

            _metrics.Measure.Gauge.SetValue(BotMetrics.MembersTotal, usersKnown.Count);
            _metrics.Measure.Gauge.SetValue(BotMetrics.MembersOnline, usersOnline.Count);
            
            // Aggregate DB stats
            var counts = await _db.Execute(c => c.QueryFirstAsync<Counts>("select (select count(*) from systems) as systems, (select count(*) from members) as members, (select count(*) from switches) as switches, (select count(*) from messages) as messages, (select count(*) from groups) as groups"));
            _metrics.Measure.Gauge.SetValue(CoreMetrics.SystemCount, counts.Systems);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.MemberCount, counts.Members);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.SwitchCount, counts.Switches);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.MessageCount, counts.Messages);
            _metrics.Measure.Gauge.SetValue(CoreMetrics.GroupCount, counts.Groups);
            
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
            _logger.Debug("Updated metrics in {Time}", stopwatch.ElapsedDuration());
        }

        public class Counts
        {
            public int Systems { get; }
            public int Members { get;  }
            public int Switches { get; }
            public int Messages { get; }
            public int Groups { get; }
        }
    }
}