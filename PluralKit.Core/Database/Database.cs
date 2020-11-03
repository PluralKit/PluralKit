using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

using App.Metrics;

using Dapper;

using NodaTime;

using Npgsql;

using Serilog;

namespace PluralKit.Core
{
    public class Database: IDatabase
    {
        private readonly CoreConfig _config;
        private readonly ILogger _logger;
        private readonly IMetrics _metrics;
        private readonly DbConnectionCountHolder _countHolder;
        private readonly string _connectionString;
        private readonly DatabaseMigrator _migrator;

        public Database(CoreConfig config, DbConnectionCountHolder countHolder, ILogger logger,
                        IMetrics metrics, DatabaseMigrator migrator)
        {
            _config = config;
            _countHolder = countHolder;
            _metrics = metrics;
            _migrator = migrator;
            _logger = logger;
            
            _connectionString = new NpgsqlConnectionStringBuilder(_config.Database)
            {
                Pooling = true, MaxPoolSize = 500, Enlist = false, NoResetOnClose = true,
                
                // Lower timeout than default (15s -> 2s), should ideally fail-fast instead of hanging
                Timeout = 2
            }.ConnectionString;
        }
        
        public static void InitStatic()
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            
            // Dapper by default tries to pass ulongs to Npgsql, which rejects them since PostgreSQL technically
            // doesn't support unsigned types on its own.
            // Instead we add a custom mapper to encode them as signed integers instead, converting them back and forth.
            SqlMapper.RemoveTypeMap(typeof(ulong));
            SqlMapper.AddTypeHandler(new UlongEncodeAsLongHandler());
            SqlMapper.AddTypeHandler(new UlongArrayHandler());

            NpgsqlConnection.GlobalTypeMapper.UseNodaTime();
            // With the thing we add above, Npgsql already handles NodaTime integration
            // This makes Dapper confused since it thinks it has to convert it anyway and doesn't understand the types
            // So we add a custom type handler that literally just passes the type through to Npgsql
            SqlMapper.AddTypeHandler(new PassthroughTypeHandler<Instant>());
            SqlMapper.AddTypeHandler(new PassthroughTypeHandler<LocalDate>());
            
            // Add ID types to Dapper
            SqlMapper.AddTypeHandler(new NumericIdHandler<SystemId, int>(i => new SystemId(i)));
            SqlMapper.AddTypeHandler(new NumericIdHandler<MemberId, int>(i => new MemberId(i)));
            SqlMapper.AddTypeHandler(new NumericIdHandler<SwitchId, int>(i => new SwitchId(i)));
            SqlMapper.AddTypeHandler(new NumericIdHandler<GroupId, int>(i => new GroupId(i)));
            SqlMapper.AddTypeHandler(new NumericIdArrayHandler<SystemId, int>(i => new SystemId(i)));
            SqlMapper.AddTypeHandler(new NumericIdArrayHandler<MemberId, int>(i => new MemberId(i)));
            SqlMapper.AddTypeHandler(new NumericIdArrayHandler<SwitchId, int>(i => new SwitchId(i)));
            SqlMapper.AddTypeHandler(new NumericIdArrayHandler<GroupId, int>(i => new GroupId(i)));
            
            // Register our custom types to Npgsql
            // Without these it'll still *work* but break at the first launch + probably cause other small issues
            NpgsqlConnection.GlobalTypeMapper.MapComposite<ProxyTag>("proxy_tag");
            NpgsqlConnection.GlobalTypeMapper.MapEnum<PrivacyLevel>("privacy_level");
        }

        public async Task ApplyMigrations()
        {
            await using var conn = await Obtain();
            await _migrator.ApplyMigrations(conn);
        }

        public async Task<IPKConnection> Obtain()
        {
            // Mark the request (for a handle, I guess) in the metrics
            _metrics.Measure.Meter.Mark(CoreMetrics.DatabaseRequests);
            
            // Create a connection and open it
            // We wrap it in PKConnection for tracing purposes
            var conn = new PKConnection(new NpgsqlConnection(_connectionString), _countHolder, _logger, _metrics);
            await conn.OpenAsync();
            return conn;
        }

        private class PassthroughTypeHandler<T>: SqlMapper.TypeHandler<T>
        {
            public override void SetValue(IDbDataParameter parameter, T value) => parameter.Value = value;
            public override T Parse(object value) => (T) value;
        }

        private class UlongEncodeAsLongHandler: SqlMapper.TypeHandler<ulong>
        {
            public override ulong Parse(object value) =>
                // Cast to long to unbox, then to ulong (???)
                (ulong) (long) value;

            public override void SetValue(IDbDataParameter parameter, ulong value) => parameter.Value = (long) value;
        }

        private class UlongArrayHandler: SqlMapper.TypeHandler<ulong[]>
        {
            public override void SetValue(IDbDataParameter parameter, ulong[] value) => parameter.Value = Array.ConvertAll(value, i => (long) i);

            public override ulong[] Parse(object value) => Array.ConvertAll((long[]) value, i => (ulong) i);
        }

        private class NumericIdHandler<T, TInner>: SqlMapper.TypeHandler<T>
            where T: INumericId<T, TInner>
            where TInner: IEquatable<TInner>, IComparable<TInner>
        {
            private readonly Func<TInner, T> _factory;

            public NumericIdHandler(Func<TInner, T> factory)
            {
                _factory = factory;
            }

            public override void SetValue(IDbDataParameter parameter, T value) => parameter.Value = value.Value;

            public override T Parse(object value) => _factory((TInner) value);
        }

        private class NumericIdArrayHandler<T, TInner>: SqlMapper.TypeHandler<T[]>
            where T: INumericId<T, TInner>
            where TInner: IEquatable<TInner>, IComparable<TInner>
        {
            private readonly Func<TInner, T> _factory;

            public NumericIdArrayHandler(Func<TInner, T> factory)
            {
                _factory = factory;
            }

            public override void SetValue(IDbDataParameter parameter, T[] value) => parameter.Value = Array.ConvertAll(value, v => v.Value);

            public override T[] Parse(object value) => Array.ConvertAll((TInner[]) value, v => _factory(v));
        }
    }
    
    public static class DatabaseExt
    {
        public static async Task Execute(this IDatabase db, Func<IPKConnection, Task> func)
        {
            await using var conn = await db.Obtain();
            await func(conn);
        }

        public static async Task<T> Execute<T>(this IDatabase db, Func<IPKConnection, Task<T>> func)
        {
            await using var conn = await db.Obtain();
            return await func(conn);
        }
        
        public static async IAsyncEnumerable<T> Execute<T>(this IDatabase db, Func<IPKConnection, IAsyncEnumerable<T>> func)
        {
            await using var conn = await db.Obtain();
            
            await foreach (var val in func(conn))
                yield return val;
        }
    }
}