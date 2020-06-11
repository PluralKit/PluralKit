using System;
using System.IO;
using System.Threading.Tasks;

using Dapper;

using Npgsql;

using Serilog;

namespace PluralKit.Core {
    public class SchemaService
    {
        private const int TargetSchemaVersion = 6;

        private DbConnectionFactory _conn;
        private ILogger _logger;
        
        public SchemaService(DbConnectionFactory conn, ILogger logger)
        {
            _conn = conn;
            _logger = logger.ForContext<SchemaService>();
        }

        public static void Initialize()
        {
            // Without these it'll still *work* but break at the first launch + probably cause other small issues
            NpgsqlConnection.GlobalTypeMapper.MapComposite<ProxyTag>("proxy_tag");
            NpgsqlConnection.GlobalTypeMapper.MapEnum<PrivacyLevel>("privacy_level");
        }

        public async Task ApplyMigrations()
        {
            for (var version = 0; version <= TargetSchemaVersion; version++) 
                await ApplyMigration(version);
        }

        private async Task ApplyMigration(int migrationId)
        {
            // migrationId is the *target* version
            using var conn = await _conn.Obtain();
            using var tx = conn.BeginTransaction();
            
            // See if we even have the info table... if not, we implicitly define the version as -1
            // This means migration 0 will get executed, which ensures we're at a consistent state.
            // *Technically* this also means schema version 0 will be identified as -1, but since we're only doing these
            // checks in the above for loop, this doesn't matter.
            var hasInfoTable = await conn.QuerySingleOrDefaultAsync<int>("select count(*) from information_schema.tables where table_name = 'info'") == 1;

            int currentVersion;
            if (hasInfoTable)
                currentVersion = await conn.QuerySingleOrDefaultAsync<int>("select schema_version from info");
            else currentVersion = -1;
            
            if (currentVersion >= migrationId)
                return; // Don't execute the migration if we're already at the target version.

            using var stream = typeof(SchemaService).Assembly.GetManifestResourceStream($"PluralKit.Core.Migrations.{migrationId}.sql");
            if (stream == null) throw new ArgumentException("Invalid migration ID");
            
            using var reader = new StreamReader(stream);
            var migrationQuery = await reader.ReadToEndAsync();
            
            _logger.Information("Current schema version is {CurrentVersion}, applying migration {MigrationId}", currentVersion, migrationId);
            await conn.ExecuteAsync(migrationQuery, transaction: tx);
            tx.Commit();
            
            // If the above migration creates new enum/composite types, we must tell Npgsql to reload the internal type caches
            // This will propagate to every other connection as well, since it marks the global type mapper collection dirty.
            // TODO: find a way to get around the cast to our internal tracker wrapper... this could break if that ever changes
            ((PerformanceTrackingConnection) conn)._impl.ReloadTypes();
        }
    }
}