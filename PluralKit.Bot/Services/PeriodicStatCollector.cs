using System.Diagnostics;

using App.Metrics;

using Myriad.Cache;

using Newtonsoft.Json;

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
    private readonly BotConfig _botConfig;
    private readonly CoreConfig _config;

    private readonly ILogger _logger;
    private readonly IMetrics _metrics;

    private readonly ModelRepository _repo;
    private readonly RedisService _redis;

    private readonly WebhookCacheService _webhookCache;

    public PeriodicStatCollector(IMetrics metrics, ILogger logger, WebhookCacheService webhookCache,
                                 DbConnectionCountHolder countHolder, CpuStatService cpu, ModelRepository repo,
                                 BotConfig botConfig, CoreConfig config, RedisService redis, IDiscordCache cache)
    {
        _metrics = metrics;
        _webhookCache = webhookCache;
        _countHolder = countHolder;
        _cpu = cpu;
        _repo = repo;
        _cache = cache;
        _botConfig = botConfig;
        _config = config;
        _redis = redis;
        _logger = logger.ForContext<PeriodicStatCollector>();
    }

    public async Task CollectStats()
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // Process info
        var process = Process.GetCurrentProcess();
        _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessPhysicalMemory, process.WorkingSet64);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessVirtualMemory, process.VirtualMemorySize64);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessPrivateMemory, process.PrivateMemorySize64);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessThreads, process.Threads.Count);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.ProcessHandles, process.HandleCount);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.CpuUsage, await _cpu.EstimateCpuUsage());

        stopwatch.Stop();
        _logger.Debug("Updated metrics in {Time}", stopwatch.ElapsedDuration());
    }
}