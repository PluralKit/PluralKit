#nullable enable
using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

using App.Metrics;

using NodaTime;

using Npgsql;

using Serilog;

namespace PluralKit.Core
{
    public class PKCommand: DbCommand, IPKCommand
    {
        private readonly NpgsqlCommand _inner;
        private readonly PKConnection _ourConnection;
        private readonly ILogger _logger;
        private readonly IMetrics _metrics;
        
        public PKCommand(NpgsqlCommand inner, PKConnection ourConnection, ILogger logger, IMetrics metrics)
        {
            _inner = inner;
            _ourConnection = ourConnection;
            _logger = logger.ForContext<PKCommand>();
            _metrics = metrics;
        }

        public override int ExecuteNonQuery() => throw SyncError(nameof(ExecuteNonQuery));
        public override object ExecuteScalar() => throw SyncError(nameof(ExecuteScalar));
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw SyncError(nameof(ExecuteDbDataReader));

        public override Task<int> ExecuteNonQueryAsync(CancellationToken ct) => LogQuery(_inner.ExecuteNonQueryAsync(ct));
        public override Task<object> ExecuteScalarAsync(CancellationToken ct) => LogQuery(_inner.ExecuteScalarAsync(ct));
        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken ct) => await LogQuery(_inner.ExecuteReaderAsync(behavior, ct));

        public override void Prepare() => _inner.Prepare();
        public override void Cancel() => _inner.Cancel();
        protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

        public override string CommandText
        {
            get => _inner.CommandText;
            set => _inner.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _inner.CommandTimeout;
            set => _inner.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _inner.CommandType;
            set => _inner.CommandType = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _inner.UpdatedRowSource;
            set => _inner.UpdatedRowSource = value;
        }

        protected override DbParameterCollection DbParameterCollection => _inner.Parameters;
        protected override DbTransaction? DbTransaction
        {
            get => _inner.Transaction;
            set => _inner.Transaction = (NpgsqlTransaction?) value;
        }

        public override bool DesignTimeVisible
        {
            get => _inner.DesignTimeVisible;
            set => _inner.DesignTimeVisible = value;
        }

        protected override DbConnection? DbConnection
        {
            get => _inner.Connection;
            set =>
                _inner.Connection = value switch
                {
                    NpgsqlConnection npg => npg,
                    PKConnection pk => pk.Inner,
                    _ => throw new ArgumentException($"Can't convert input type {value?.GetType()} to NpgsqlConnection")
                };
        }
        
        private async Task<T> LogQuery<T>(Task<T> task)
        {
            var start = SystemClock.Instance.GetCurrentInstant();
            try
            {
                return await task;
            }
            finally
            {
                var end = SystemClock.Instance.GetCurrentInstant();
                var elapsed = end - start;
                
                _logger.Verbose("Executed query {Query} in {ElapsedTime} on connection {ConnectionId}", CommandText, elapsed, _ourConnection.ConnectionId);
                
                // One "BCL compatible tick" is 100 nanoseconds
                var micros = elapsed.BclCompatibleTicks / 10;
                _metrics.Provider.Timer.Instance(CoreMetrics.DatabaseQuery, new MetricTags("query", CommandText))
                    .Record(micros, TimeUnit.Microseconds, CommandText);
            }
        }
        
        private static Exception SyncError(string caller) => throw new Exception($"Executed synchronous IPKCommand function {caller}!");
    }
}