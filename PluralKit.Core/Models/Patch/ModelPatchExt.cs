using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public static class ModelPatchExt
    {
        public static Task<PKSystem> UpdateSystem(this IPKConnection conn, SystemId id, SystemPatch patch)
        {
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Update("systems", "id = @id"))
                .WithConstant("id", id)
                .Build("returning *");
            return conn.QueryFirstAsync<PKSystem>(query, pms);
        }

        public static Task DeleteSystem(this IPKConnection conn, SystemId id) =>
            conn.ExecuteAsync("delete from systems where id = @Id", new {Id = id});

        public static Task<PKMember> CreateMember(this IPKConnection conn, SystemId system, string memberName) =>
            conn.QueryFirstAsync<PKMember>(
                "insert into members (hid, system, name) values (find_free_member_hid(), @SystemId, @Name) returning *",
                new {SystemId = system, Name = memberName});

        public static Task<PKMember> UpdateMember(this IPKConnection conn, MemberId id, MemberPatch patch)
        {
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Update("members", "id = @id"))
                .WithConstant("id", id)
                .Build("returning *");
            return conn.QueryFirstAsync<PKMember>(query, pms);
        }
        
        public static Task DeleteMember(this IPKConnection conn, MemberId id) =>
            conn.ExecuteAsync("delete from members where id = @Id", new {Id = id});

        public static Task UpsertSystemGuild(this IPKConnection conn, SystemId system, ulong guild,
                                             SystemGuildPatch patch)
        {
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Upsert("system_guild", "system, guild"))
                .WithConstant("system", system)
                .WithConstant("guild", guild)
                .Build();
            return conn.ExecuteAsync(query, pms);
        }
        
        public static Task UpsertMemberGuild(this IPKConnection conn, MemberId member, ulong guild,
                                             MemberGuildPatch patch)
        {
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Upsert("member_guild", "member, guild"))
                .WithConstant("member", member)
                .WithConstant("guild", guild)
                .Build();
            return conn.ExecuteAsync(query, pms);
        }
        
        public static Task UpsertGuild(this IPKConnection conn, ulong guild, GuildPatch patch)
        {
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Upsert("servers", "id"))
                .WithConstant("id", guild)
                .Build();
            return conn.ExecuteAsync(query, pms);
        }
        
        public static Task<PKGroup> CreateGroup(this IPKConnection conn, SystemId system, string name) =>
            conn.QueryFirstAsync<PKGroup>(
                "insert into groups (hid, system, name) values (find_free_group_hid(), @System, @Name) returning *",
                new {System = system, Name = name});
        
        public static Task<PKGroup> UpdateGroup(this IPKConnection conn, GroupId id, GroupPatch patch)
        {
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Update("groups", "id = @id"))
                .WithConstant("id", id)
                .Build("returning *");
            return conn.QueryFirstAsync<PKGroup>(query, pms);
        }
    }
}