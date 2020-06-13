using System;
using System.Data;
using System.Threading.Tasks;

using App.Metrics;

using Dapper;

using NodaTime;

using Npgsql;

using Serilog;

namespace PluralKit.Core
{
    public class Database
    {
        private readonly CoreConfig _config;
        private readonly ILogger _logger;
        private readonly IMetrics _metrics;
        private readonly DbConnectionCountHolder _countHolder;

        public Database(CoreConfig config, DbConnectionCountHolder countHolder, ILogger logger,
                        IMetrics metrics)
        {
            _config = config;
            _countHolder = countHolder;
            _metrics = metrics;
            _logger = logger;
        }
        
        public static void InitStatic()
        {
            // Dapper by default tries to pass ulongs to Npgsql, which rejects them since PostgreSQL technically
            // doesn't support unsigned types on its own.
            // Instead we add a custom mapper to encode them as signed integers instead, converting them back and forth.
            SqlMapper.RemoveTypeMap(typeof(ulong));
            SqlMapper.AddTypeHandler(new UlongEncodeAsLongHandler());
            SqlMapper.AddTypeHandler(new UlongArrayHandler());
            DefaultTypeMap.MatchNamesWithUnderscores = true;

            NpgsqlConnection.GlobalTypeMapper.UseNodaTime();
            // With the thing we add above, Npgsql already handles NodaTime integration
            // This makes Dapper confused since it thinks it has to convert it anyway and doesn't understand the types
            // So we add a custom type handler that literally just passes the type through to Npgsql
            SqlMapper.AddTypeHandler(new PassthroughTypeHandler<Instant>());
            SqlMapper.AddTypeHandler(new PassthroughTypeHandler<LocalDate>());
            
            // Register our custom types to Npgsql
            // Without these it'll still *work* but break at the first launch + probably cause other small issues
            NpgsqlConnection.GlobalTypeMapper.MapComposite<ProxyTag>("proxy_tag");
            NpgsqlConnection.GlobalTypeMapper.MapEnum<PrivacyLevel>("privacy_level");
        }

        public async Task<IPKConnection> Obtain()
        {
            // Mark the request (for a handle, I guess) in the metrics
            _metrics.Measure.Meter.Mark(CoreMetrics.DatabaseRequests);
            
            // Create a connection and open it
            // We wrap it in PKConnection for tracing purposes
            var conn = new PKConnection(new NpgsqlConnection(_config.Database), _countHolder, _logger, _metrics);
            await conn.OpenAsync();
            return conn;
        }
        
        public async Task Execute(Func<IPKConnection, Task> func)
        {
            await using var conn = await Obtain();
            await func(conn);
        }
        
        public async Task<T> Execute<T>(Func<IPKConnection, Task<T>> func)
        {
            await using var conn = await Obtain();
            return await func(conn);
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
    }
}