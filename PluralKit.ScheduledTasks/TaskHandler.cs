using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using NodaTime;
using NodaTime.Extensions;

using PluralKit.Core;

using Serilog;

namespace PluralKit.ScheduledTasks;

public class TaskHandler
{
    private static readonly Duration CommandMessageRetention = Duration.FromHours(24);
    private readonly IDatabase _db;

    private readonly ILogger _logger;
    private readonly ModelRepository _repo;
    private Timer _periodicTask;

    public TaskHandler(ILogger logger, IDatabase db, ModelRepository repo)
    {
        _logger = logger;
        _db = db;
        _repo = repo;
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

        // Clean up message cache in postgres
        await CleanupOldMessages();

        stopwatch.Stop();
        _logger.Information("Ran scheduled tasks in {Time}", stopwatch.ElapsedDuration());
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