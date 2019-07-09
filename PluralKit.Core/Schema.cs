using System.Data;
using System.IO;
using System.Threading.Tasks;
using Dapper;

namespace PluralKit {
    public static class Schema {
        public static async Task CreateTables(IDbConnection connection)
        {
            // Load the schema from disk (well, embedded resource) and execute the commands in there
            using (var stream = typeof(Schema).Assembly.GetManifestResourceStream("PluralKit.Core.db_schema.sql"))
            using (var reader = new StreamReader(stream))
            {
                var result = await reader.ReadToEndAsync();
                await connection.ExecuteAsync(result);
            }
        }
    }
}