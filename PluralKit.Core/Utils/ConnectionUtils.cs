using System.Collections.Generic;
using System.Data.Common;

using Dapper;

namespace PluralKit.Core {
    public static class ConnectionUtils
    {
        public static async IAsyncEnumerable<T> QueryStreamAsync<T>(this IDatabase connFactory, string sql, object param)
        {
            await using var conn = await connFactory.Obtain();
            
            await using var reader = (DbDataReader) await conn.ExecuteReaderAsync(sql, param);
            var parser = reader.GetRowParser<T>();
            while (await reader.ReadAsync())
                yield return parser(reader); 
        }
        
        public static async IAsyncEnumerable<T> QueryStreamAsync<T>(this IPKConnection conn, string sql, object param)
        {
            await using var reader = (DbDataReader) await conn.ExecuteReaderAsync(sql, param);
            var parser = reader.GetRowParser<T>();
            while (await reader.ReadAsync())
                yield return parser(reader); 
        }
    }
}