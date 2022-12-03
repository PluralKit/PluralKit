using App.Metrics;

using NodaTime;

namespace PluralKit.API;

public class MetricsRunner: IHostedService, IDisposable
{
    private readonly Serilog.ILogger _logger;
    private readonly IMetrics _metrics;

    private Timer? _periodicTask = null;

    public MetricsRunner(Serilog.ILogger logger, IMetrics metrics)
    {
        _logger = logger;
        _metrics = metrics;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        var timeNow = SystemClock.Instance.GetCurrentInstant();
        var timeTillNextWholeMinute = TimeSpan.FromMilliseconds(60000 - timeNow.ToUnixTimeMilliseconds() % 60000 + 250);
        _periodicTask = new Timer(_ =>
        {
            var __ = ReportMetrics();
        }, null, timeTillNextWholeMinute, TimeSpan.FromMinutes(1));

        return Task.CompletedTask;
    }

    private async Task ReportMetrics()
    {
        await Task.WhenAll(((IMetricsRoot)_metrics).ReportRunner.RunAllAsync());
        _logger.Debug("Submitted metrics to backend");
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        _periodicTask.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _periodicTask?.Dispose();
    }
}