#nullable enable
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public Task<PKGroup?> GetGroupByName(IPKConnection conn, SystemId system, string name) =>
            conn.QueryFirstOrDefaultAsync<PKGroup?>("select * from groups where system = @System and lower(Name) = lower(@Name)", new {System = system, Name = name});
        
        public Task<PKGroup?> GetGroupByDisplayName(IPKConnection conn, SystemId system, string display_name) =>
            conn.QueryFirstOrDefaultAsync<PKGroup?>("select * from groups where system = @System and lower(display_name) = lower(@Name)", new {System = system, Name = display_name});
        
        public Task<PKGroup?> GetGroupByHid(IPKConnection conn, string hid) =>
            conn.QueryFirstOrDefaultAsync<PKGroup?>("select * from groups where hid = @hid", new {hid = hid.ToLowerInvariant()});
        
        public Task<int> GetGroupMemberCount(IPKConnection conn, GroupId id, PrivacyLevel? privacyFilter = null)
        {
            var query = new StringBuilder("select count(*) from group_members");
            if (privacyFilter != null)
                query.Append(" inner join members on group_members.member_id = members.id");
            query.Append(" where group_members.group_id = @Id");
            if (privacyFilter != null)
                query.Append(" and members.member_visibility = @PrivacyFilter");
            return conn.QuerySingleOrDefaultAsync<int>(query.ToString(), new {Id = id, PrivacyFilter = privacyFilter});
        }
        
        public async Task<PKGroup> CreateGroup(IPKConnection conn, SystemId system, string name, IDbTransaction? transaction = null)
        {
            var group = await conn.QueryFirstAsync<PKGroup>(
                "insert into groups (hid, system, name) values (find_free_group_hid(), @System, @Name) returning *",
                new {System = system, Name = name}, transaction);
            _logger.Information("Created group {GroupId} in system {SystemId}: {GroupName}", group.Id, system, name);
            return group;
        }

        public Task<PKGroup> UpdateGroup(IPKConnection conn, GroupId id, GroupPatch patch, IDbTransaction? transaction = null)
        {
            _logger.Information("Updated {GroupId}: {@GroupPatch}", id, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Update("groups", "id = @id"))
                .WithConstant("id", id)
                .Build("returning *");
            return conn.QueryFirstAsync<PKGroup>(query, pms, transaction);
        }

        public Task DeleteGroup(IPKConnection conn, GroupId group)
        {
            _logger.Information("Deleted {GroupId}", group);
            return conn.ExecuteAsync("delete from groups where id = @Id", new {Id = @group});
        }

        public async Task AddMembersToGroup(IPKConnection conn, GroupId group,
                                            IReadOnlyCollection<MemberId> members)
        {
            await using var w =
                conn.BeginBinaryImport("copy group_members (group_id, member_id) from stdin (format binary)");
            foreach (var member in members)
            {
                await w.StartRowAsync();
                await w.WriteAsync(group.Value);
                await w.WriteAsync(member.Value);
            }

            await w.CompleteAsync();
            _logger.Information("Added members to {GroupId}: {MemberIds}", group, members);
        }

        public Task RemoveMembersFromGroup(IPKConnection conn, GroupId group,
                                           IReadOnlyCollection<MemberId> members)
        {
            _logger.Information("Removed members from {GroupId}: {MemberIds}", group, members);
            return conn.ExecuteAsync("delete from group_members where group_id = @Group and member_id = any(@Members)",
                new {Group = @group, Members = members.ToArray()});
        }
    }
}