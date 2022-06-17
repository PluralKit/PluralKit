#nullable enable
using Dapper;

using SqlKata;

namespace PluralKit.Core;

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
        var query = new Query("accounts")
            .Select("systems.*")
            .LeftJoin("systems", "systems.id", "accounts.system")
            .Where("uid", accountId)
            .WhereNotNull("system");
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
        var system = await _db.QueryFirst<PKSystem>(conn, query, "returning *");
        _logger.Information("Created {SystemId}", system.Id);

        var (q, pms) = ("insert into system_config (system) values (@system)", new { system = system.Id });

        if (conn == null)
            await _db.Execute(conn => conn.QueryAsync(q, pms));
        else
            await conn.QueryAsync(q, pms);

        // no dispatch call here - system was just created, we don't have a webhook URL
        return system;
    }

    public async Task<PKSystem> UpdateSystem(SystemId id, SystemPatch patch, IPKConnection? conn = null)
    {
        _logger.Information("Updated {SystemId}: {@SystemPatch}", id, patch);
        var query = patch.Apply(new Query("systems").Where("id", id));
        var res = await _db.QueryFirst<PKSystem>(conn, query, "returning *");

        _ = _dispatch.Dispatch(id, new UpdateDispatchData
        {
            Event = DispatchEvent.UPDATE_SYSTEM,
            EventData = patch.ToJson(),
        });

        return res;
    }

    public async Task AddAccount(SystemId system, ulong accountId, IPKConnection? conn = null)
    {
        // We have "on conflict do nothing" since linking an account when it's already linked to the same system is idempotent
        // This is used in import/export, although the sp;link command checks for this case beforehand

        // update 2022-01: the accounts table is now independent of systems
        // we MUST check for the presence of a system before inserting, or it will move the new account to the current system

        var query = new Query("accounts").AsInsert(new { system, uid = accountId });
        await _db.ExecuteQuery(conn, query, "on conflict (uid) do update set system = @p0");

        _logger.Information("Linked account {UserId} to {SystemId}", accountId, system);

        _ = _dispatch.Dispatch(system, new UpdateDispatchData
        {
            Event = DispatchEvent.LINK_ACCOUNT,
            EntityId = accountId.ToString(),
        });
    }

    public async Task RemoveAccount(SystemId system, ulong accountId)
    {
        var query = new Query("accounts").AsUpdate(new
        {
            system = (ulong?)null
        }).Where("uid", accountId).Where("system", system);
        await _db.ExecuteQuery(query);
        _logger.Information("Unlinked account {UserId} from {SystemId}", accountId, system);
        _ = _dispatch.Dispatch(system, new UpdateDispatchData
        {
            Event = DispatchEvent.UNLINK_ACCOUNT,
            EntityId = accountId.ToString(),
        });
    }

    public async Task DeleteSystem(SystemId id)
    {
        var query = new Query("systems").AsDelete().Where("id", id);
        await _db.ExecuteQuery(query);
        _logger.Information("Deleted {SystemId}", id);
    }
}