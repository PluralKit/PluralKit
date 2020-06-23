#nullable enable
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public static class ModelQueryExt
    {
        public static Task<PKSystem?> QuerySystem(this IPKConnection conn, SystemId id) =>
            conn.QueryFirstOrDefaultAsync<PKSystem?>("select * from systems where id = @id", new {id});
        
        public static Task<PKMember?> QueryMember(this IPKConnection conn, MemberId id) =>
            conn.QueryFirstOrDefaultAsync<PKMember?>("select * from members where id = @id", new {id});
        
        public static Task<GuildConfig> QueryOrInsertGuildConfig(this IPKConnection conn, ulong guild) =>
            conn.QueryFirstAsync<GuildConfig>("insert into servers (id) values (@guild) on conflict (id) do update set id = @guild returning *", new {guild});

        public static Task<SystemGuildSettings> QueryOrInsertSystemGuildConfig(this IPKConnection conn, ulong guild, SystemId system) =>
            conn.QueryFirstAsync<SystemGuildSettings>(
                "insert into member_guild (guild, member) values (@guild, @member) on conflict (guild, member) do update set guild = @guild, member = @member returning *", 
                new {guild, system});

        public static Task<MemberGuildSettings> QueryOrInsertMemberGuildConfig(
            this IPKConnection conn, ulong guild, MemberId member) =>
            conn.QueryFirstAsync<MemberGuildSettings>(
                "insert into member_guild (guild, member) values (@guild, @member) on conflict (guild, member) do update set guild = @guild, member = @member returning *",
                new {guild, member});
    }
}