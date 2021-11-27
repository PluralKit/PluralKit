using System.Diagnostics;

using App.Metrics;

using Myriad.Cache;

using NodaTime.Extensions;

using PluralKit.Core;

using Serilog;

using Stopwatch = System.Diagnostics.Stopwatch;

namespace PluralKit.Bot;

public class PeriodicStatCollector
{
    private readonly IDiscordCache _cache;

    private readonly DbConnectionCountHolder _countHolder;
    private readonly CpuStatService _cpu;

    private readonly ILogger _logger;
    private readonly IMetrics _metrics;

    private readonly ModelRepository _repo;

    private readonly WebhookCacheService _webhookCache;

    public PeriodicStatCollector(IMetrics metrics, ILogger logger, WebhookCacheService webhookCache,
                                 DbConnectionCountHolder countHolder, CpuStatService cpu, ModelRepository repo,
                                 IDiscordCache cache)
    {
        _metrics = metrics;
        _webhookCache = webhookCache;
        _countHolder = countHolder;
        _cpu = cpu;
        _repo = repo;
        _cache = cache;
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
        await foreach (var guild in _cache.GetAllGuilds())
        {
            guildCount++;
            foreach (var channel in await _cache.GetGuildChannels(guild.Id))
                if (DiscordUtils.IsValidGuildChannel(channel))
                    channelCount++;
        }

        _metrics.Measure.Gauge.SetValue(BotMetrics.Guilds, guildCount);
        _metrics.Measure.Gauge.SetValue(BotMetrics.Channels, channelCount);

        // Aggregate DB stats
        // just fetching from database here - actual updating of the data is done in PluralKit.ScheduledTasks
        // if you're not running ScheduledTasks and want up-to-date counts, uncomment the following line:
        // await _repo.UpdateStats();
        var counts = await _repo.GetStats();
        _metrics.Measure.Gauge.SetValue(CoreMetrics.SystemCount, counts.SystemCount);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.MemberCount, counts.MemberCount);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.GroupCount, counts.GroupCount);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.SwitchCount, counts.SwitchCount);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.MessageCount, counts.MessageCount);

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
}