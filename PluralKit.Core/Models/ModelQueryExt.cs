using System;
using System.Data;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public static class ModelQueryExt
    {
        public static Task<GuildConfig> QueryOrInsertGuildConfig(this IDbConnection conn, ulong guild) =>
            conn.QueryFirstOrDefaultAsync<GuildConfig>("insert into servers (id) values (@Guild) on conflict do nothing returning *", new {Guild = guild});
    }
}