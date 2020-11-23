using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {        
        public IAsyncEnumerable<PKGroup> GetMemberGroups(IPKConnection conn, MemberId id) =>
            conn.QueryStreamAsync<PKGroup>(
                "select groups.* from group_members inner join groups on group_members.group_id = groups.id where group_members.member_id = @Id",
                new {Id = id});
        

        public async Task AddGroupsToMember(IPKConnection conn, MemberId member, IReadOnlyCollection<GroupId> groups)
        {
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

        public Task RemoveGroupsFromMember(IPKConnection conn, MemberId member, IReadOnlyCollection<GroupId> groups)
        {
            _logger.Information("Removed groups from {MemberId}: {GroupIds}", member, groups);
            return conn.ExecuteAsync("delete from group_members where member_id = @Member and group_id = any(@Groups)",
                new {Member = @member, Groups = groups.ToArray() });
        }

    }
}