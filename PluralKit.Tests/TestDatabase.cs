using System.Threading.Tasks;

using Dapper;

using Npgsql;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Tests
{
    public class TestDatabase: IDatabase
    {
        private const string DatabaseName = "pluralkit_test";
        
        private readonly CoreConfig _cfg;
        private readonly DatabaseMigrator _migrator;
        private readonly ILogger _logger;

        public string ConnectionString =>
            new NpgsqlConnectionStringBuilder(_cfg.Database) {Database = DatabaseName}.ConnectionString;
        
        public TestDatabase(CoreConfig cfg, ILogger logger)
        {
            _cfg = cfg;
            _logger = logger;
            _migrator = new DatabaseMigrator(logger);
            Database.InitStatic();
        }

        public async Task ApplyMigrations()
        {
            await using var connOutsideTest = new NpgsqlConnection(_cfg.Database);
            await connOutsideTest.ExecuteAsync($"drop database if exists {DatabaseName}");
            await connOutsideTest.ExecuteAsync($"create database {DatabaseName} with owner = 'postgres' encoding = 'utf8' connection limit = -1");

            await using var connInsideTest = await Obtain();
            await _migrator.ApplyMigrations(connInsideTest);
        }

        public async Task<IPKConnection> Obtain()
        {
            var npgsqlConn = new NpgsqlConnection(ConnectionString);
            await npgsqlConn.OpenAsync();

            return new PKConnection(npgsqlConn, new DbConnectionCountHolder(), _logger, null);
        }
    }
}