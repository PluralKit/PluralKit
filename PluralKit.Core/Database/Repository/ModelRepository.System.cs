#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using SqlKata;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public Task<PKSystem?> GetSystem(SystemId id)
        {
            var query = new Query("systems").Where("id", id);
            return _db.QueryFirst<PKSystem?>(query);
        }

        public Task<PKSystem?> GetSystemByGuid(Guid id)
        {
            var query = new Query("systems").Where("uuid", id);
            return _db.QueryFirst<PKSystem?>(query);
        }

        public Task<PKSystem?> GetSystemByAccount(ulong accountId)
        {
            var query = new Query("accounts").Select("systems.*").LeftJoin("systems", "systems.id", "accounts.system", "=").Where("uid", accountId);
            return _db.QueryFirst<PKSystem?>(query);
        }

        public Task<PKSystem?> GetSystemByHid(string hid)
        {
            var query = new Query("systems").Where("hid", hid.ToLower());
            return _db.QueryFirst<PKSystem?>(query);
        }

        public Task<IEnumerable<ulong>> GetSystemAccounts(SystemId system)
        {
            var query = new Query("accounts").Select("uid").Where("system", system);
            return _db.Query<ulong>(query);
        }

        public IAsyncEnumerable<PKMember> GetSystemMembers(SystemId system)
        {
            var query = new Query("members").Where("system", system);
            return _db.QueryStream<PKMember>(query);
        }

        public IAsyncEnumerable<PKGroup> GetSystemGroups(SystemId system)
        {
            var query = new Query("groups").Where("system", system);
            return _db.QueryStream<PKGroup>(query);
        }

        public Task<int> GetSystemMemberCount(SystemId system, PrivacyLevel? privacyFilter = null)
        {
            var query = new Query("members").SelectRaw("count(*)").Where("system", system);
            if (privacyFilter != null)
                query.Where("member_visibility", (int)privacyFilter.Value);

            return _db.QueryFirst<int>(query);
        }

        public Task<int> GetSystemGroupCount(SystemId system, PrivacyLevel? privacyFilter = null)
        {
            var query = new Query("groups").SelectRaw("count(*)").Where("system", system);
            if (privacyFilter != null)
                query.Where("visibility", (int)privacyFilter.Value);

            return _db.QueryFirst<int>(query);
        }

        public async Task<PKSystem> CreateSystem(string? systemName = null, IPKConnection? conn = null)
        {
            var query = new Query("systems").AsInsert(new
            {
                hid = new UnsafeLiteral("find_free_system_hid()"),
                name = systemName
            });
            var system = await _db.QueryFirst<PKSystem>(conn, query, extraSql: "returning *");
            _logger.Information("Created {SystemId}", system.Id);

            // no dispatch call here - system was just created, we don't have a webhook URL
            return system;
        }

        public async Task<PKSystem> UpdateSystem(SystemId id, SystemPatch patch, IPKConnection? conn = null)
        {
            _logger.Information("Updated {SystemId}: {@SystemPatch}", id, patch);
            var query = patch.Apply(new Query("systems").Where("id", id));
            var res = await _db.QueryFirst<PKSystem>(conn, query, extraSql: "returning *");

            _ = _dispatch.Dispatch(id, new UpdateDispatchData()
            {
                Event = DispatchEvent.UPDATE_SYSTEM,
                EventData = patch.ToJson(),
            });

            return res;
        }

        public async Task AddAccount(SystemId system, ulong accountId, IPKConnection? conn = null)
        {
            // We have "on conflict do nothing" since linking an account when it's already linked to the same system is idempotent
            // This is used in import/export, although the pk;link command checks for this case beforehand

            var query = new Query("accounts").AsInsert(new
            {
                system = system,
                uid = accountId,
            });

            _logger.Information("Linked account {UserId} to {SystemId}", accountId, system);
            await _db.ExecuteQuery(conn, query, extraSql: "on conflict do nothing");

            _ = _dispatch.Dispatch(system, new UpdateDispatchData()
            {
                Event = DispatchEvent.LINK_ACCOUNT,
                EntityId = accountId.ToString(),
            });
        }

        public async Task RemoveAccount(SystemId system, ulong accountId)
        {
            var query = new Query("accounts").AsDelete().Where("uid", accountId).Where("system", system);
            await _db.ExecuteQuery(query);
            _logger.Information("Unlinked account {UserId} from {SystemId}", accountId, system);
            _ = _dispatch.Dispatch(system, new UpdateDispatchData()
            {
                Event = DispatchEvent.UNLINK_ACCOUNT,
                EntityId = accountId.ToString(),
            });
        }

        public Task DeleteSystem(SystemId id)
        {
            var query = new Query("systems").AsDelete().Where("id", id);
            _logger.Information("Deleted {SystemId}", id);
            return _db.ExecuteQuery(query);
        }
    }
}