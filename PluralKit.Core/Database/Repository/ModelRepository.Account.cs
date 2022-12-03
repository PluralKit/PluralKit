using Dapper;

using SqlKata;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public async Task<ulong?> GetDmChannel(ulong id)
        => await _db.QueryFirst<ulong?>(new Query("accounts").Select("dm_channel").Where("uid", id));

    public async Task<bool> GetAutoproxyEnabled(ulong id)
        => await _db.QueryFirst<bool>(new Query("accounts").Select("allow_autoproxy").Where("uid", id));

    public async Task UpdateAccount(ulong id, AccountPatch patch)
    {
        _logger.Information("Updated account {accountId}: {@AccountPatch}", id, patch);
        var query = patch.Apply(new Query("accounts").Where("uid", id));
        _ = _dispatch.Dispatch(id, patch);
        await _db.ExecuteQuery(query, "returning *");
    }
}