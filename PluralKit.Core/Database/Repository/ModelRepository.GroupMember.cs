using System.Collections.Generic;
using System.Threading.Tasks;

using SqlKata;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public IAsyncEnumerable<PKGroup> GetMemberGroups(MemberId id)
        {
            var query = new Query("group_members")
                .Select("groups.*")
                .Join("groups", "group_members.group_id", "groups.id")
                .Where("group_members.member_id", id);
            return _db.QueryStream<PKGroup>(query);
        }

        // todo: add this to metrics tracking
        public async Task AddGroupsToMember(MemberId member, IReadOnlyCollection<GroupId> groups)
        {
            await using var conn = await _db.Obtain();
            await using var w =
                conn.BeginBinaryImport("copy group_members (group_id, member_id) from stdin (format binary)");
            foreach (var group in groups)
            {
                await w.StartRowAsync();
                await w.WriteAsync(group.Value);
                await w.WriteAsync(member.Value);
            }

            await w.CompleteAsync();
            _logger.Information("Added member {MemberId} to groups {GroupIds}", member, groups);
        }

        public Task RemoveGroupsFromMember(MemberId member, IReadOnlyCollection<GroupId> groups)
        {
            _logger.Information("Removed groups from {MemberId}: {GroupIds}", member, groups);
            var query = new Query("group_members").AsDelete()
                .Where("member_id", member)
                .WhereIn("group_id", groups);
            return _db.ExecuteQuery(query);
        }

        // todo: add this to metrics tracking
        public async Task AddMembersToGroup(GroupId group, IReadOnlyCollection<MemberId> members)
        {
            await using var conn = await _db.Obtain();
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

        public Task RemoveMembersFromGroup(GroupId group, IReadOnlyCollection<MemberId> members)
        {
            _logger.Information("Removed members from {GroupId}: {MemberIds}", group, members);
            var query = new Query("group_members").AsDelete()
                .Where("group_id", group)
                .WhereIn("member_id", members);
            return _db.ExecuteQuery(query);
        }
    }
}