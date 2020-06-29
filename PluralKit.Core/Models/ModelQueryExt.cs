#nullable enable
using System.Threading.Tasks;

using Dapper;

using PluralKit.Core;

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
                "insert into system_guild (guild, system) values (@guild, @system) on conflict (guild, system) do update set guild = @guild, system = @system returning *", 
                new {guild, system});

        public static Task<MemberGuildSettings> QueryOrInsertMemberGuildConfig(
            this IPKConnection conn, ulong guild, MemberId member) =>
            conn.QueryFirstAsync<MemberGuildSettings>(
                "insert into member_guild (guild, member) values (@guild, @member) on conflict (guild, member) do update set guild = @guild, member = @member returning *",
                new {guild, member});

        public static Task<PKMember> UpdateMember(this IPKConnection conn, MemberId id, MemberPatch patch)
        {
            var (query, pms) = patch.Apply(new UpdateQueryBuilder("members", "id = @id"))
                .WithConstant("id", id)
                .Build("returning *");
            return conn.QueryFirstAsync<PKMember>(query, pms);
        }

        public static Task DeleteMember(this IPKConnection conn, MemberId id) =>
            conn.ExecuteAsync("delete from members where id = @Id", new {Id = id});
    }
}