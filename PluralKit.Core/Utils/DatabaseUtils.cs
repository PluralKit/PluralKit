using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using App.Metrics;

using Dapper;

using Npgsql;

using Serilog;

namespace PluralKit.Core
{
    public class QueryLogger : IDisposable
    {
        private ILogger _logger;
        private IMetrics _metrics;
        private string _commandText;
        private Stopwatch _stopwatch;

        public QueryLogger(ILogger logger, IMetrics metrics, string commandText)
        {
            _metrics = metrics;
            _commandText = commandText;
            _logger = logger;
            
            _stopwatch = new Stopwatch();
            _stopwatch.Start();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.Verbose("Executed query {Query} in {ElapsedTime}", _commandText, _stopwatch.Elapsed);
            
            // One tick is 100 nanoseconds
            _metrics.Provider.Timer.Instance(CoreMetrics.DatabaseQuery, new MetricTags("query", _commandText))
                .Record(_stopwatch.ElapsedTicks / 10, TimeUnit.Microseconds, _commandText);
        }
    }

    public class PerformanceTrackingCommand: DbCommand
    {
        private NpgsqlCommand _impl;
        private ILogger _logger;
        private IMetrics _metrics;

        public PerformanceTrackingCommand(NpgsqlCommand impl, ILogger logger, IMetrics metrics)
        {
            _impl = impl;
            _metrics = metrics;
            _logger = logger;
        }

        public override void Cancel()
        {
            _impl.Cancel();
        }

        public override int ExecuteNonQuery()
        {
            return _impl.ExecuteNonQuery();
        }

        public override object ExecuteScalar()
        {
            return _impl.ExecuteScalar();
        }

        public override void Prepare()
        {
            _impl.Prepare();
        }

        public override string CommandText
        {
            get => _impl.CommandText;
            set => _impl.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _impl.CommandTimeout;
            set => _impl.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _impl.CommandType;
            set => _impl.CommandType = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _impl.UpdatedRowSource;
            set => _impl.UpdatedRowSource = value;
        }

        protected override DbConnection DbConnection
        {
            get => _impl.Connection;
            set => _impl.Connection = (NpgsqlConnection) value;
        }

        protected override DbParameterCollection DbParameterCollection => _impl.Parameters;

        protected override DbTransaction DbTransaction
        {
            get => _impl.Transaction;
            set => _impl.Transaction = (NpgsqlTransaction) value;
        }

        public override bool DesignTimeVisible
        {
            get => _impl.DesignTimeVisible;
            set => _impl.DesignTimeVisible = value;
        }

        protected override DbParameter CreateDbParameter()
        {
            return _impl.CreateParameter();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            return _impl.ExecuteReader(behavior);
        }

        private IDisposable LogQuery()
        {
            return new QueryLogger(_logger, _metrics, CommandText);
        }

        protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior, CancellationToken cancellationToken)
        {
            using (LogQuery())
                return await _impl.ExecuteReaderAsync(behavior, cancellationToken);
        }

        public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            using (LogQuery())
                return await _impl.ExecuteNonQueryAsync(cancellationToken);
        }

        public override async Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            using (LogQuery())
                return await _impl.ExecuteScalarAsync(cancellationToken);
        }
    }

    public class PerformanceTrackingConnection: IAsyncDbConnection
    {
        // Simple delegation of everything.
        internal NpgsqlConnection _impl;

        private DbConnectionCountHolder _countHolder;
        private ILogger _logger;
        private IMetrics _metrics;

        public PerformanceTrackingConnection(NpgsqlConnection impl, DbConnectionCountHolder countHolder,
                                             ILogger logger, IMetrics metrics)
        {
            _impl = impl;
            _countHolder = countHolder;
            _logger = logger;
            _metrics = metrics;
        }

        public void Dispose()
        {
            _impl.Dispose();

            _countHolder.Decrement();
        }

        public IDbTransaction BeginTransaction()
        {
            return _impl.BeginTransaction();
        }

        public IDbTransaction BeginTransaction(IsolationLevel il)
        {
            return _impl.BeginTransaction(il);
        }

        public void ChangeDatabase(string databaseName)
        {
            _impl.ChangeDatabase(databaseName);
        }

        public void Close()
        {
            _impl.Close();
        }

        public IDbCommand CreateCommand()
        {
            return new PerformanceTrackingCommand(_impl.CreateCommand(), _logger, _metrics);
        }

        public void Open()
        {
            _impl.Open();
        }

        public NpgsqlBinaryImporter BeginBinaryImport(string copyFromCommand)
        {
            return _impl.BeginBinaryImport(copyFromCommand);
        }

        public string ConnectionString
        {
            get => _impl.ConnectionString;
            set => _impl.ConnectionString = value;
        }

        public int ConnectionTimeout => _impl.ConnectionTimeout;

        public string Database => _impl.Database;

        public ConnectionState State => _impl.State;
        public ValueTask DisposeAsync() => _impl.DisposeAsync();
    }

    public class DbConnectionCountHolder
    {
        private int _connectionCount;
        public int ConnectionCount => _connectionCount;

        public void Increment()
        {
            Interlocked.Increment(ref _connectionCount);
        }

        public void Decrement()
        {
            Interlocked.Decrement(ref _connectionCount);
        }
    }

    public interface IAsyncDbConnection: IDbConnection, IAsyncDisposable
    {
        
    }

    public class DbConnectionFactory
    {
        private CoreConfig _config;
        private ILogger _logger;
        private IMetrics _metrics;
        private DbConnectionCountHolder _countHolder;

        public DbConnectionFactory(CoreConfig config, DbConnectionCountHolder countHolder, ILogger logger,
                                   IMetrics metrics)
        {
            _config = config;
            _countHolder = countHolder;
            _metrics = metrics;
            _logger = logger;
        }

        public async Task<IAsyncDbConnection> Obtain()
        {
            // Mark the request (for a handle, I guess) in the metrics
            _metrics.Measure.Meter.Mark(CoreMetrics.DatabaseRequests);

            // Actually create and try to open the connection
            var conn = new NpgsqlConnection(_config.Database);
            await conn.OpenAsync();

            // Increment the count
            _countHolder.Increment();
            // Return a wrapped connection which will decrement the counter on dispose
            return new PerformanceTrackingConnection(conn, _countHolder, _logger, _metrics);
        }
    }

    public class PassthroughTypeHandler<T>: SqlMapper.TypeHandler<T>
    {
        public override void SetValue(IDbDataParameter parameter, T value)
        {
            parameter.Value = value;
        }

        public override T Parse(object value)
        {
            return (T) value;
        }
    }

    public class UlongEncodeAsLongHandler: SqlMapper.TypeHandler<ulong>
    {
        public override ulong Parse(object value)
        {
            // Cast to long to unbox, then to ulong (???)
            return (ulong) (long) value;
        }

        public override void SetValue(IDbDataParameter parameter, ulong value)
        {
            parameter.Value = (long) value;
        }
    }

    public static class DatabaseExt
    {
        public static async Task<T> Execute<T>(this DbConnectionFactory db, Func<IDbConnection, Task<T>> func)
        {
            await using var conn = await db.Obtain();
            return await func(conn);
        }
    }
}