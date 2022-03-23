using System;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using App.Metrics;

using NodaTime;
using NodaTime.Extensions;

using Newtonsoft.Json;

using PluralKit.Core;

using Serilog;

namespace PluralKit.ScheduledTasks;

public class TaskHandler
{
    private static readonly Duration CommandMessageRetention = Duration.FromHours(24);
    private readonly IDatabase _db;
    private readonly RedisService _redis;
    private readonly bool _useRedisMetrics;

    private readonly ILogger _logger;
    private readonly IMetrics _metrics;
    private readonly ModelRepository _repo;
    private Timer _periodicTask;

    public TaskHandler(ILogger logger, IMetrics metrics, CoreConfig config, IDatabase db, RedisService redis, ModelRepository repo)
    {
        _logger = logger;
        _metrics = metrics;
        _db = db;
        _redis = redis;
        _repo = repo;

        _useRedisMetrics = config.UseRedisMetrics;
    }

    public void Run()
    {
        _logger.Information("Starting scheduled task runner...");
        var timeNow = SystemClock.Instance.GetCurrentInstant();
        var timeTillNextWholeMinute =
            TimeSpan.FromMilliseconds(60000 - timeNow.ToUnixTimeMilliseconds() % 60000 + 250);
        _periodicTask = new Timer(_ =>
        {
            var __ = UpdatePeriodic();
        }, null, timeTillNextWholeMinute, TimeSpan.FromMinutes(1));
    }

    private async Task UpdatePeriodic()
    {
        _logger.Information("Running per-minute scheduled tasks.");
        var stopwatch = new Stopwatch();
        stopwatch.Start();

        _logger.Information("Updating database stats...");
        await _repo.UpdateStats();

        // Collect bot cluster statistics from Redis (if it's enabled)
        if (_useRedisMetrics)
            await CollectBotStats();

        // Clean up message cache in postgres
        await CleanupOldMessages();

        stopwatch.Stop();
        _logger.Information("Ran scheduled tasks in {Time}", stopwatch.ElapsedDuration());
    }

    private async Task CollectBotStats()
    {
        var redisStats = await _redis.Connection.GetDatabase().HashGetAllAsync("pluralkit:cluster_stats");

        var stats = redisStats.Select(v => JsonConvert.DeserializeObject<ClusterMetricInfo>(v.Value));

        _metrics.Measure.Gauge.SetValue(Metrics.Guilds, stats.Sum(x => x.GuildCount));
        _metrics.Measure.Gauge.SetValue(Metrics.Channels, stats.Sum(x => x.ChannelCount));

        // Aggregate DB stats
        // just fetching from database here - actual updating of the data is done elsewiere
        var counts = await _repo.GetStats();
        _metrics.Measure.Gauge.SetValue(CoreMetrics.SystemCount, counts.SystemCount);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.MemberCount, counts.MemberCount);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.GroupCount, counts.GroupCount);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.SwitchCount, counts.SwitchCount);
        _metrics.Measure.Gauge.SetValue(CoreMetrics.MessageCount, counts.MessageCount);

        // Database info
        // this is pretty much always inaccurate but oh well
        _metrics.Measure.Gauge.SetValue(CoreMetrics.DatabaseConnections, stats.Sum(x => x.DatabaseConnectionCount));

        // Other shiz
        _metrics.Measure.Gauge.SetValue(Metrics.WebhookCacheSize, stats.Sum(x => x.WebhookCacheSize));

        _logger.Debug("Submitted metrics to backend");
    }

    private async Task CleanupOldMessages()
    {
        var deleteThresholdInstant = SystemClock.Instance.GetCurrentInstant() - CommandMessageRetention;
        var deleteThresholdSnowflake = InstantToSnowflake(deleteThresholdInstant);

        var deletedRows = await _repo.DeleteCommandMessagesBefore(deleteThresholdSnowflake);

        _logger.Information(
            "Pruned {DeletedRows} command messages older than retention {Retention} (older than {DeleteThresholdInstant} / {DeleteThresholdSnowflake})",
            deletedRows, CommandMessageRetention, deleteThresholdInstant, deleteThresholdSnowflake);
    }

    // we don't have access to PluralKit.Bot here, so this needs to be vendored
    public static ulong InstantToSnowflake(Instant time) =>
        (ulong)(time - Instant.FromUtc(2015, 1, 1, 0, 0, 0)).TotalMilliseconds << 22;
}