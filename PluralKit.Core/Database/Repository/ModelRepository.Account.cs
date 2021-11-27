using SqlKata;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public async Task UpdateAccount(ulong id, AccountPatch patch)
    {
        _logger.Information("Updated account {accountId}: {@AccountPatch}", id, patch);
        var query = patch.Apply(new Query("accounts").Where("uid", id));
        _ = _dispatch.Dispatch(id, patch);
        await _db.ExecuteQuery(query, "returning *");
    }
}