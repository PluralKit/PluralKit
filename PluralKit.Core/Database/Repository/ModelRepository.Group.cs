#nullable enable
using System;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public Task<PKGroup?> GetGroup(GroupId id)
        {
            var query = new Query("groups").Where("id", id);
            return _db.QueryFirst<PKGroup?>(query);
        }

        public Task<PKGroup?> GetGroupByName(SystemId system, string name)
        {
            var query = new Query("groups").Where("system", system).WhereRaw("lower(name) = lower(?)", name.ToLower());
            return _db.QueryFirst<PKGroup?>(query);
        }

        public Task<PKGroup?> GetGroupByDisplayName(SystemId system, string display_name)
        {
            var query = new Query("groups").Where("system", system).WhereRaw("lower(display_name) = lower(?)", display_name.ToLower());
            return _db.QueryFirst<PKGroup?>(query);
        }

        public Task<PKGroup?> GetGroupByHid(string hid, SystemId? system = null)
        {
            var query = new Query("groups").Where("hid", hid.ToLower());
            if (system != null)
                query = query.Where("system", system);
            return _db.QueryFirst<PKGroup?>(query);
        }

        public Task<PKGroup?> GetGroupByGuid(Guid uuid)
        {
            var query = new Query("groups").Where("uuid", uuid);
            return _db.QueryFirst<PKGroup?>(query);
        }

        public Task<int> GetGroupMemberCount(GroupId id, PrivacyLevel? privacyFilter = null)
        {
            var query = new Query("group_members")
                .SelectRaw("count(*)")
                .Where("group_members.group_id", id);

            if (privacyFilter != null) query = query
                .Join("members", "group_members.member_id", "members.id")
                .Where("members.member_visibility", privacyFilter);

            return _db.QueryFirst<int>(query);
        }

        public async Task<PKGroup> CreateGroup(SystemId system, string name, IPKConnection? conn = null)
        {
            var query = new Query("groups").AsInsert(new
            {
                hid = new UnsafeLiteral("find_free_group_hid()"),
                system = system,
                name = name
            });
            var group = await _db.QueryFirst<PKGroup>(conn, query, extraSql: "returning *");
            _logger.Information("Created group {GroupId} in system {SystemId}: {GroupName}", group.Id, system, name);
            return group;
        }

        public async Task<PKGroup> UpdateGroup(GroupId id, GroupPatch patch, IPKConnection? conn = null)
        {
            _logger.Information("Updated {GroupId}: {@GroupPatch}", id, patch);
            var query = patch.Apply(new Query("groups").Where("id", id));
            var group = await _db.QueryFirst<PKGroup>(conn, query, extraSql: "returning *");

            if (conn == null)
                _ = _dispatch.Dispatch(id, new()
                {
                    Event = DispatchEvent.UPDATE_GROUP,
                    EventData = patch.ToJson(),
                });
            return group;
        }

        public async Task DeleteGroup(GroupId group)
        {
            var oldGroup = await GetGroup(group);

            _logger.Information("Deleted {GroupId}", group);
            var query = new Query("groups").AsDelete().Where("id", group);
            await _db.ExecuteQuery(query);

            if (oldGroup != null)
                _ = _dispatch.Dispatch(oldGroup.System, oldGroup.Uuid, DispatchEvent.DELETE_GROUP);
        }
    }
}