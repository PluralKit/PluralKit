using System.Data;

using Dapper;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Matrix;

public class MatrixMigrator
{
    private const string RootPath = "PluralKit.Matrix.Database";
    private const int TargetSchemaVersion = 1;
    private readonly ILogger _logger;

    public MatrixMigrator(ILogger logger)
    {
        _logger = logger.ForContext<MatrixMigrator>();
    }

    public async Task ApplyMigrations(IPKConnection conn)
    {
        await using var tx = await conn.BeginTransactionAsync();

        // Drop existing matrix functions before reapplying
        await ExecuteSqlFile($"{RootPath}.Functions.clean.sql", conn, tx);

        // Apply any pending migrations
        await ApplyMigrationsInner(conn, tx);

        // Reapply functions (may evolve without new migrations)
        await ExecuteSqlFile($"{RootPath}.Functions.matrix_functions.sql", conn, tx);

        await tx.CommitAsync();
    }

    private async Task ApplyMigrationsInner(IPKConnection conn, IDbTransaction tx)
    {
        var currentVersion = await GetCurrentSchemaVersion(conn);
        _logger.Information("Matrix schema version: {CurrentVersion}", currentVersion);

        for (var migration = currentVersion + 1; migration <= TargetSchemaVersion; migration++)
        {
            _logger.Information("Applying Matrix schema migration {MigrationId}", migration);
            await ExecuteSqlFile($"{RootPath}.Migrations.{migration}.sql", conn, tx);
        }
    }

    private async Task ExecuteSqlFile(string resourceName, IPKConnection conn, IDbTransaction tx = null)
    {
        await using var stream = typeof(MatrixMigrator).Assembly.GetManifestResourceStream(resourceName);
        if (stream == null) throw new ArgumentException($"Invalid resource name '{resourceName}'");

        using var reader = new StreamReader(stream);
        var query = await reader.ReadToEndAsync();

        await conn.ExecuteAsync(query, transaction: tx);
    }

    private async Task<int> GetCurrentSchemaVersion(IPKConnection conn)
    {
        var hasTable = await conn.QuerySingleOrDefaultAsync<int>(
            "select count(*) from information_schema.tables where table_name = 'matrix_schema_info'") == 1;

        if (hasTable)
            return await conn.QuerySingleOrDefaultAsync<int>("select schema_version from matrix_schema_info");

        return -1;
    }
}
