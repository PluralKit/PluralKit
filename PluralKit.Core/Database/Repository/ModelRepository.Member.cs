#nullable enable
using System;
using System.Threading.Tasks;

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
            return _db.QueryFirst<PKMember>(conn, query);
        }

        public Task DeleteMember(MemberId id)
        {
            _logger.Information("Deleted {MemberId}", id);
            var query = new Query("members").AsDelete().Where("id", id);
            return _db.ExecuteQuery(query);
        }
    }
}