#nullable enable
using System;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public Task<PKMember?> GetMember(IPKConnection conn, MemberId id) =>
            conn.QueryFirstOrDefaultAsync<PKMember?>("select * from members where id = @id", new {id});
        
        public Task<PKMember?> GetMemberByHid(IPKConnection conn, string hid) => 
            conn.QuerySingleOrDefaultAsync<PKMember?>("select * from members where hid = @Hid", new { Hid = hid.ToLower() });
        
        public Task<PKMember?> GetMemberByGuid(IPKConnection conn, Guid guid) => 
            conn.QuerySingleOrDefaultAsync<PKMember?>("select * from members where uuid = @Uuid", new { Uuid = guid });

        public Task<PKMember?> GetMemberByName(IPKConnection conn, SystemId system, string name) => 
            conn.QueryFirstOrDefaultAsync<PKMember?>("select * from members where lower(name) = lower(@Name) and system = @SystemID", new { Name = name, SystemID = system });

        public Task<PKMember?> GetMemberByDisplayName(IPKConnection conn, SystemId system, string name) => 
            conn.QueryFirstOrDefaultAsync<PKMember?>("select * from members where lower(display_name) = lower(@Name) and system = @SystemID", new { Name = name, SystemID = system });

        public async Task<PKMember> CreateMember(IPKConnection conn, SystemId id, string memberName)
        {
            var member = await conn.QueryFirstAsync<PKMember>(
                "insert into members (hid, system, name) values (find_free_member_hid(), @SystemId, @Name) returning *",
                new {SystemId = id, Name = memberName});
            _logger.Information("Created {MemberId} in {SystemId}: {MemberName}",
                member.Id, id, memberName);
            return member;
        }

        public Task<PKMember> UpdateMember(IPKConnection conn, MemberId id, MemberPatch patch)
        {
            _logger.Information("Updated {MemberId}: {@MemberPatch}", id, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Update("members", "id = @id"))
                .WithConstant("id", id)
                .Build("returning *");
            return conn.QueryFirstAsync<PKMember>(query, pms);
        }

        public Task DeleteMember(IPKConnection conn, MemberId id)
        {
            _logger.Information("Deleted {MemberId}", id);
            return conn.ExecuteAsync("delete from members where id = @Id", new {Id = id});
        }
    }
}