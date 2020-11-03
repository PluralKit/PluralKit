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
    internal class PKCommand: DbCommand, IPKCommand
    {
        private NpgsqlCommand Inner { get; }
        
        private readonly PKConnection _ourConnection;
        private readonly ILogger _logger;
        private readonly IMetrics? _metrics;
        
        public PKCommand(NpgsqlCommand inner, PKConnection ourConnection, ILogger logger, IMetrics? metrics)
        {
            Inner = inner;
            _ourConnection = ourConnection;
            _logger = logger.ForContext<PKCommand>();
            _metrics = metrics;
        }

        public override Task<int> ExecuteNonQueryAsync(CancellationToken ct) => LogQuery(Inner.ExecuteNonQueryAsync(ct));
        public override Task<object> ExecuteScalarAsync(CancellationToken ct) => LogQuery(Inner.ExecuteScalarAsync(ct));
        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken ct) => await LogQuery(Inner.ExecuteReaderAsync(behavior, ct));

        public override Task PrepareAsync(CancellationToken ct = default) => Inner.PrepareAsync(ct);
        public override void Cancel() => Inner.Cancel();
        protected override DbParameter CreateDbParameter() => Inner.CreateParameter();

        public override string CommandText
        {
            get => Inner.CommandText;
            set => Inner.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => Inner.CommandTimeout;
            set => Inner.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => Inner.CommandType;
            set => Inner.CommandType = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => Inner.UpdatedRowSource;
            set => Inner.UpdatedRowSource = value;
        }

        protected override DbParameterCollection DbParameterCollection => Inner.Parameters;
        protected override DbTransaction? DbTransaction
        {
            get => Inner.Transaction;
            set => Inner.Transaction = value switch
            {
                NpgsqlTransaction npg => npg,
                PKTransaction pk => pk.Inner,
                _ => throw new ArgumentException($"Can't convert input type {value?.GetType()} to NpgsqlTransaction")
            };
        }

        public override bool DesignTimeVisible
        {
            get => Inner.DesignTimeVisible;
            set => Inner.DesignTimeVisible = value;
        }

        protected override DbConnection? DbConnection
        {
            get => Inner.Connection;
            set =>
                Inner.Connection = value switch
                {
                    NpgsqlConnection npg => npg,
                    PKConnection pk => pk.Inner,
                    _ => throw new ArgumentException($"Can't convert input type {value?.GetType()} to NpgsqlConnection")
                };
        }

        public override int ExecuteNonQuery() => throw SyncError(nameof(ExecuteNonQuery));
        public override object ExecuteScalar() => throw SyncError(nameof(ExecuteScalar));
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw SyncError(nameof(ExecuteDbDataReader));
        public override void Prepare() => throw SyncError(nameof(Prepare));

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
                if (_metrics != null)
                    _metrics.Provider.Timer.Instance(CoreMetrics.DatabaseQuery, new MetricTags("query", CommandText))
                        .Record(micros, TimeUnit.Microseconds, CommandText);
            }
        }
        
        private static Exception SyncError(string caller) => throw new Exception($"Executed synchronous IDbCommand function {caller}!");
    }
}