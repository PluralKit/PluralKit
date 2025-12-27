using SqlKata;
using Npgsql;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task<SystemConfig> GetSystemConfig(SystemId system, IPKConnection conn = null)
        => _db.QueryFirst<SystemConfig>(conn, new Query("system_config").Where("system", system));

    public async Task<SystemConfig> UpdateSystemConfig(SystemId system, SystemConfigPatch patch, IPKConnection conn = null)
    {
        var query = patch.Apply(new Query("system_config").Where("system", system));
        var config = await _db.QueryFirst<SystemConfig>(conn, query, "returning *");

        _ = _dispatch.Dispatch(system, new UpdateDispatchData
        {
            Event = DispatchEvent.UPDATE_SETTINGS,
            EventData = patch.ToJson()
        });

        return config;
    }

    public async Task<bool> TryUpdateSystemConfigForIdChange(SystemId system, IPKConnection conn = null)
    {
        var query = new Query("system_config")
            .AsUpdate(new
            {
                premium_id_changes_remaining = new UnsafeLiteral("premium_id_changes_remaining - 1")
            })
            .Where("system", system);

        try
        {
            await _db.ExecuteQuery(conn, query);
        }
        catch (PostgresException pe)
        {
            if (!pe.Message.Contains("violates check constraint"))
                throw;
            return false;
        }

        return true;
    }
}