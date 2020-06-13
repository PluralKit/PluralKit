using System;
using System.Threading.Tasks;

using App.Metrics;

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
    }
}