using SqlKata;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public async Task<SystemConfig> GetSystemConfig(SystemId system, IPKConnection conn = null)
    {
        var cfg = await _db.QueryFirst<SystemConfig>(conn, new Query("system_config").Where("system", system));
        var trustedUsers = await _db.Query<ulong>(new Query("trusted_users").Select("uid").Where("system", system));
        var trustedGuilds = await _db.Query<ulong>(new Query("trusted_guilds").Select("guild").Where("system", system));
        cfg.Trusted = (trustedUsers, trustedGuilds);
        return cfg;
    }


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

    public async Task AddTrustedUser(SystemId system, ulong accountId, IPKConnection conn = null)
    {
        var query = new Query("trusted_users").AsInsert(new { system, uid = accountId });
        await _db.ExecuteQuery(conn, query, "on conflict (system, uid) do update set system = @p0");

        _ = _dispatch.Dispatch(system, new UpdateDispatchData
        {
            Event = DispatchEvent.ADD_TRUSTED_USER,
            EntityId = accountId.ToString(),
        });
    }

    public async Task RemoveTrustedUser(SystemId system, ulong accountId)
    {
        var query = new Query("trusted_users").AsDelete().Where("system", system).Where("uid", accountId);
        await _db.ExecuteQuery(query);

        _ = _dispatch.Dispatch(system, new UpdateDispatchData
        {
            Event = DispatchEvent.REMOVE_TRUSTED_USER,
            EntityId = accountId.ToString(),
        });
    }

    public async Task<bool> GetTrustedUserRelation(SystemId system, ulong accountId)
    {
        var query = new Query("trusted_users").SelectRaw("count(*)").Where("system", system.Value).Where("uid", accountId);
        var relation = await _db.QueryFirst<int>(query);
        if (relation > 0)
            return true;
        return false;
    }

    public async Task AddTrustedGuild(SystemId system, ulong guild, IPKConnection conn = null)
    {
        var query = new Query("trusted_guilds").AsInsert(new { system, guild });
        await _db.ExecuteQuery(conn, query, "on conflict (system, guild) do update set system = @p0");

        _ = _dispatch.Dispatch(system, new UpdateDispatchData
        {
            Event = DispatchEvent.ADD_TRUSTED_GUILD,
            EntityId = guild.ToString(),
        });
    }

    public async Task RemoveTrustedGuild(SystemId system, ulong guild)
    {
        var query = new Query("trusted_guilds").AsDelete().Where("system", system).Where("guild", guild);
        await _db.ExecuteQuery(query);

        _ = _dispatch.Dispatch(system, new UpdateDispatchData
        {
            Event = DispatchEvent.REMOVE_TRUSTED_GUILD,
            EntityId = guild.ToString(),
        });
    }

    public async Task<bool> GetTrustedGuildRelation(SystemId system, ulong guild)
    {
        var query = new Query("trusted_guilds").SelectRaw("count(*)").Where("system", system.Value).Where("guild", guild);
        var relation = await _db.QueryFirst<int>(query);
        if (relation > 0)
            return true;
        return false;
    }
}