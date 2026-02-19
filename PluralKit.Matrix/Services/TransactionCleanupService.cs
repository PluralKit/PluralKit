using Dapper;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Matrix;

public class TransactionCleanupService : IHostedService, IDisposable
{
    private readonly IDatabase _db;
    private readonly ILogger _logger;
    private Timer? _timer;

    public TransactionCleanupService(IDatabase db, ILogger logger)
    {
        _db = db;
        _logger = logger.ForContext<TransactionCleanupService>();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(_ => _ = CleanupAsync(), null, TimeSpan.FromMinutes(5), TimeSpan.FromHours(1));
        return Task.CompletedTask;
    }

    private async Task CleanupAsync()
    {
        try
        {
            await using var conn = await _db.Obtain();
            var deleted = await conn.ExecuteAsync(
                "delete from matrix_transactions where processed_at < now() - interval '7 days'");
            if (deleted > 0)
                _logger.Information("Cleaned up {Count} old Matrix transaction records", deleted);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to clean up old Matrix transactions");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
