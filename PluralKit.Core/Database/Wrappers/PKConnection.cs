#nullable enable
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using App.Metrics;

using NodaTime;

using Npgsql;

using Serilog;

namespace PluralKit.Core;

internal class PKConnection: DbConnection, IPKConnection
{
    private readonly DbConnectionCountHolder _countHolder;
    private readonly ILogger _logger;
    private readonly IMetrics _metrics;
    private bool _hasClosed;

    private bool _hasOpened;
    private Instant _openTime;

    public PKConnection(NpgsqlConnection inner, DbConnectionCountHolder countHolder, ILogger logger,
                        IMetrics metrics)
    {
        Inner = inner;
        ConnectionId = Guid.NewGuid();
        _countHolder = countHolder;
        _logger = logger.ForContext<PKConnection>();
        _metrics = metrics;
    }

    public NpgsqlConnection Inner { get; }
    public override string DataSource => Inner.DataSource;
    public override string ServerVersion => Inner.ServerVersion;
    public Guid ConnectionId { get; }

    public override Task OpenAsync(CancellationToken ct)
    {
        if (_hasOpened) return Inner.OpenAsync(ct);
        _countHolder.Increment();
        _hasOpened = true;
        _openTime = SystemClock.Instance.GetCurrentInstant();
        _logger.Verbose("Opened database connection {ConnectionId}, new connection count {ConnectionCount}",
            ConnectionId, _countHolder.ConnectionCount);
        return Inner.OpenAsync(ct);
    }

    public override Task CloseAsync() => Inner.CloseAsync();

    public new async ValueTask<IPKTransaction>
        BeginTransactionAsync(IsolationLevel level, CancellationToken ct = default) =>
        new PKTransaction(await Inner.BeginTransactionAsync(level, ct));

    public NpgsqlBinaryImporter BeginBinaryImport(string copyFromCommand) =>
        Inner.BeginBinaryImport(copyFromCommand);

    public NpgsqlBinaryExporter BeginBinaryExport(string copyToCommand) => Inner.BeginBinaryExport(copyToCommand);

    public override void ChangeDatabase(string databaseName) => Inner.ChangeDatabase(databaseName);

    public override Task ChangeDatabaseAsync(string databaseName, CancellationToken ct = default) =>
        Inner.ChangeDatabaseAsync(databaseName, ct);

    public override void Open() => throw SyncError(nameof(Open));

    public override void Close()
    {
        // Don't throw SyncError here, Dapper calls sync Close() internally so that sucks
        Inner.Close();
    }

    IDbTransaction IPKConnection.BeginTransaction() => throw SyncError(nameof(BeginTransaction));

    IDbTransaction IPKConnection.BeginTransaction(IsolationLevel level) =>
        throw SyncError(nameof(BeginTransaction));

    [AllowNull]
    public override string ConnectionString
    {
        get => Inner.ConnectionString;
        set => Inner.ConnectionString = value;
    }

    public override string Database => Inner.Database!;
    public override ConnectionState State => Inner.State;

    public override ValueTask DisposeAsync()
    {
        if (_hasClosed) return Inner.DisposeAsync();
        LogClose();
        return Inner.DisposeAsync();
    }

    protected override DbCommand CreateDbCommand() => new PKCommand(Inner.CreateCommand(), this, _logger, _metrics);

    public void ReloadTypes() => Inner.ReloadTypes();

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw SyncError(nameof(BeginDbTransaction));

    protected override async ValueTask<DbTransaction>
        BeginDbTransactionAsync(IsolationLevel level, CancellationToken ct) =>
        new PKTransaction(await Inner.BeginTransactionAsync(level, ct));

    protected override void Dispose(bool disposing)
    {
        Inner.Dispose();
        if (_hasClosed) return;

        LogClose();
    }

    private void LogClose()
    {
        _countHolder.Decrement();
        _hasClosed = true;

        var duration = SystemClock.Instance.GetCurrentInstant() - _openTime;
        _logger.Verbose(
            "Closed database connection {ConnectionId} (open for {ConnectionDuration}), new connection count {ConnectionCount}",
            ConnectionId, duration, _countHolder.ConnectionCount);
    }

    private static Exception SyncError(string caller) =>
        throw new Exception($"Executed synchronous IDbCommand function {caller}!");
}