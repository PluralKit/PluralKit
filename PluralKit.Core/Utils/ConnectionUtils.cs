using System.Collections.Generic;
using System.Data;
using System.Data.Common;

using Dapper;

namespace PluralKit.Core {
    public static class ConnectionUtils
    {
        public static async IAsyncEnumerable<T> QueryStreamAsync<T>(this DbConnectionFactory connFactory, string sql, object param)
        {
            using var conn = await connFactory.Obtain();
            
            await using var reader = (DbDataReader) await conn.ExecuteReaderAsync(sql, param);
            var parser = reader.GetRowParser<T>();
            while (reader.Read())
                yield return parser(reader); 
        }
        
        public static async IAsyncEnumerable<T> QueryStreamAsync<T>(this IDbConnection conn, string sql, object param)
        {
            await using var reader = (DbDataReader) await conn.ExecuteReaderAsync(sql, param);
            var parser = reader.GetRowParser<T>();
            while (reader.Read())
                yield return parser(reader); 
        }
    }
}