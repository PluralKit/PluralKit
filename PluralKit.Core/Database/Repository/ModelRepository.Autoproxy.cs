using Dapper;

using SqlKata;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task<AutoproxySettings> UpdateAutoproxy(SystemId system, ulong? guildId, ulong? channelId, AutoproxyPatch patch)
    {
        var locationStr = guildId != null ? "guild" : (channelId != null ? "channel" : "global");
        _logger.Information("Updated autoproxy for {SystemId} in location {location}: {@AutoproxyPatch}", system, locationStr, patch);

        var query = patch.Apply(new Query("autoproxy")
            .Where("system", system)
            .Where("guild_id", guildId ?? 0)
            .Where("channel_id", channelId ?? 0)
        );
        _ = _dispatch.Dispatch(system, guildId, channelId, patch);
        return _db.QueryFirst<AutoproxySettings>(query, "returning *");
    }

    // todo: this might break with differently scoped autoproxy
    public async Task<AutoproxySettings> GetAutoproxySettings(SystemId system, ulong? guildId, ulong? channelId)
        => await _db.QueryFirst<AutoproxySettings>(new Query("autoproxy").AsInsert(new
        {
            system = system,
            guild_id = guildId ?? 0,
            channel_id = channelId ?? 0,
        })
            .Where("system", system)
            .Where("guild_id", guildId ?? 0)
            .Where("channel_id", channelId ?? 0),
            "on conflict (system, guild_id, channel_id) do update set system = $1 returning *"
        );
}