using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;

using Dapper;

using Npgsql;

using Serilog;

namespace PluralKit.Core
{
    public class Schemas
    {
        private const string RootPath = "PluralKit.Core.Database"; // "resource path" root for SQL files
        private const int TargetSchemaVersion = 7;

        private DbConnectionFactory _conn;
        private ILogger _logger;

        public Schemas(DbConnectionFactory conn, ILogger logger)
        {
            _conn = conn;
            _logger = logger.ForContext<Schemas>();
        }

        public static void Initialize()
        {
            // Without these it'll still *work* but break at the first launch + probably cause other small issues
            NpgsqlConnection.GlobalTypeMapper.MapComposite<ProxyTag>("proxy_tag");
            NpgsqlConnection.GlobalTypeMapper.MapEnum<PrivacyLevel>("privacy_level");
        }

        public async Task InitializeDatabase()
        {
            // Run everything in a transaction
            await using var conn = await _conn.Obtain();
            using var tx = conn.BeginTransaction();
            
            // Before applying migrations, clean out views/functions to prevent type errors
            await ExecuteSqlFile($"{RootPath}.clean.sql", conn, tx);

            // Apply all migrations between the current database version and the target version
            await ApplyMigrations(conn, tx);

            // Now, reapply views/functions (we deleted them above, no need to worry about conflicts)
            await ExecuteSqlFile($"{RootPath}.views.sql", conn, tx);
            await ExecuteSqlFile($"{RootPath}.Functions.functions.sql", conn, tx);

            // Finally, commit tx
            tx.Commit();
        }

        private async Task ApplyMigrations(IAsyncDbConnection conn, IDbTransaction tx)
        {
            var currentVersion = await GetCurrentDatabaseVersion(conn);
            _logger.Information("Current schema version: {CurrentVersion}", currentVersion);
            for (var migration = currentVersion + 1; migration <= TargetSchemaVersion; migration++)
            {
                _logger.Information("Applying schema migration {MigrationId}", migration);
                await ExecuteSqlFile($"{RootPath}.Migrations.{migration}.sql", conn, tx);
            }
        }

        private async Task ExecuteSqlFile(string resourceName, IDbConnection conn, IDbTransaction tx = null)
        {
            await using var stream = typeof(Schemas).Assembly.GetManifestResourceStream(resourceName);
            if (stream == null) throw new ArgumentException($"Invalid resource name  '{resourceName}'");

            using var reader = new StreamReader(stream);
            var query = await reader.ReadToEndAsync();

            await conn.ExecuteAsync(query, transaction: tx);

            // If the above creates new enum/composite types, we must tell Npgsql to reload the internal type caches
            // This will propagate to every other connection as well, since it marks the global type mapper collection dirty.
            // TODO: find a way to get around the cast to our internal tracker wrapper... this could break if that ever changes
            ((PerformanceTrackingConnection) conn)._impl.ReloadTypes();
        }

        private async Task<int> GetCurrentDatabaseVersion(IDbConnection conn)
        {
            // First, check if the "info" table exists (it may not, if this is a *really* old database)
            var hasInfoTable =
                await conn.QuerySingleOrDefaultAsync<int>(
                    "select count(*) from information_schema.tables where table_name = 'info'") == 1;

            // If we have the table, read the schema version
            if (hasInfoTable)
                return await conn.QuerySingleOrDefaultAsync<int>("select schema_version from info");

            // If not, we return version "-1"
            // This means migration 0 will get executed, getting us into a consistent state
            // Then, migration 1 gets executed, which creates the info table and sets version to 1
            return -1;
        }
    }
}