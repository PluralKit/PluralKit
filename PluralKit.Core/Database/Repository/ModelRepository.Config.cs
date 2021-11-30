using SqlKata;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task<SystemConfig> GetSystemConfig(SystemId system)
        => _db.QueryFirst<SystemConfig>(new Query("config").Where("system", system));

    public async Task<SystemConfig> UpdateSystemConfig(SystemId system, SystemConfigPatch patch)
    {
        var query = patch.Apply(new Query("config").Where("system", system));
        var config = await _db.QueryFirst<SystemConfig>(query, "returning *");

        _ = _dispatch.Dispatch(system, new UpdateDispatchData
        {
            Event = DispatchEvent.UPDATE_SETTINGS,
            EventData = patch.ToJson()
        });

        return config;
    }
}