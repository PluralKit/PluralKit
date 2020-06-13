#nullable enable
using System;
using System.Data;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public static class ModelQueryExt
    {
        public static Task<PKMember?> QueryMember(this IDbConnection conn, int id) =>
            conn.QueryFirstOrDefaultAsync<PKMember?>("select * from members where id = @id", new {id});
        
        public static Task<GuildConfig> QueryOrInsertGuildConfig(this IDbConnection conn, ulong guild) =>
            conn.QueryFirstAsync<GuildConfig>("insert into servers (id) values (@Guild) on conflict do nothing returning *", new {Guild = guild});

        public static Task<SystemGuildSettings> QueryOrInsertSystemGuildConfig(this IDbConnection conn, ulong guild, int system) =>
            conn.QueryFirstAsync<SystemGuildSettings>(
                "insert into member_guild (guild, member) values (@guild, @member) on conflict (guild, member) do update set guild = @guild, member = @member returning *", 
                new {guild, system});

        public static Task<MemberGuildSettings> QueryOrInsertMemberGuildConfig(
            this IDbConnection conn, ulong guild, int member) =>
            conn.QueryFirstAsync<MemberGuildSettings>(
                "insert into member_guild (guild, member) values (@guild, @member) on conflict (guild, member) do update set guild = @guild, member = @member returning *",
                new {guild, member});
    }
}