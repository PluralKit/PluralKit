#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public Task<PKMember?> GetMember(MemberId id)
        {
            var query = new Query("members").Where("id", id);
            return _db.QueryFirst<PKMember?>(query);
        }

        public Task<PKMember?> GetMemberByHid(string hid, SystemId? system = null)
        {
            var query = new Query("members").Where("hid", hid.ToLower());
            if (system != null)
                query = query.Where("system", system);
            return _db.QueryFirst<PKMember?>(query);
        }

        public Task<PKMember?> GetMemberByGuid(Guid uuid)
        {
            var query = new Query("members").Where("uuid", uuid);
            return _db.QueryFirst<PKMember?>(query);
        }

        public Task<PKMember?> GetMemberByName(SystemId system, string name)
        {
            var query = new Query("members").WhereRaw(
                "lower(name) = lower(?)",
                name.ToLower()
            ).Where("system", system);
            return _db.QueryFirst<PKMember?>(query);
        }

        public Task<PKMember?> GetMemberByDisplayName(SystemId system, string name)
        {
            var query = new Query("members").WhereRaw(
                "lower(display_name) = lower(?)",
                name.ToLower()
            ).Where("system", system);
            return _db.QueryFirst<PKMember?>(query);
        }

        public Task<IEnumerable<Guid>> GetMemberGuids(IEnumerable<MemberId> ids)
        {
            var query = new Query("members")
                .Select("uuid")
                .WhereIn("id", ids);

            return _db.Query<Guid>(query);
        }

        public async Task<PKMember> CreateMember(SystemId systemId, string memberName, IPKConnection? conn = null)
        {
            var query = new Query("members").AsInsert(new
            {
                hid = new UnsafeLiteral("find_free_member_hid()"),
                system = systemId,
                name = memberName
            });
            var member = await _db.QueryFirst<PKMember>(conn, query, "returning *");
            _logger.Information("Created {MemberId} in {SystemId}: {MemberName}",
                member.Id, systemId, memberName);
            return member;
        }

        public Task<PKMember> UpdateMember(MemberId id, MemberPatch patch, IPKConnection? conn = null)
        {
            _logger.Information("Updated {MemberId}: {@MemberPatch}", id, patch);
            var query = patch.Apply(new Query("members").Where("id", id));

            if (conn == null)
                _ = _dispatch.Dispatch(id, new()
                {
                    Event = DispatchEvent.UPDATE_MEMBER,
                    EventData = patch.ToJson(),
                });
            return _db.QueryFirst<PKMember>(conn, query, extraSql: "returning *");
        }

        public async Task DeleteMember(MemberId id)
        {
            var oldMember = await GetMember(id);

            _logger.Information("Deleted {MemberId}", id);
            var query = new Query("members").AsDelete().Where("id", id);
            await _db.ExecuteQuery(query);

            // shh, compiler
            if (oldMember != null)
                _ = _dispatch.Dispatch(oldMember.System, oldMember.Uuid, DispatchEvent.DELETE_MEMBER);
        }
    }
}