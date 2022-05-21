using SqlKata;

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
}