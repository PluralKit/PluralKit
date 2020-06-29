using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public static class ModelPatchExt
    {
        public static Task<PKSystem> UpdateSystem(this IPKConnection conn, SystemId id, SystemPatch patch)
        {
            var (query, pms) = patch.Apply(new UpdateQueryBuilder("systems", "id = @id"))
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
            var (query, pms) = patch.Apply(new UpdateQueryBuilder("members", "id = @id"))
                .WithConstant("id", id)
                .Build("returning *");
            return conn.QueryFirstAsync<PKMember>(query, pms);
        }
        
        public static Task DeleteMember(this IPKConnection conn, MemberId id) =>
            conn.ExecuteAsync("delete from members where id = @Id", new {Id = id});
    }
}