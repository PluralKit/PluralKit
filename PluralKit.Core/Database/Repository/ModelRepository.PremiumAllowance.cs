using SqlKata;
using Npgsql;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task<PremiumAllowance?> GetPremiumAllowance(SystemId system, IPKConnection conn = null)
        => _db.QueryFirst<PremiumAllowance?>(conn, new Query("premium_allowances").Where("system", system));

    public Task CreatePremiumAllowance(SystemId system, IPKConnection conn = null)
    {
        var query = new Query("premium_allowances").AsInsert(new
        {
            system = system,
        });

        return _db.ExecuteQuery(query, "on conflict do nothing");
    }

    public async Task<PremiumAllowance> UpdatePremiumAllowance(SystemId system, PremiumAllowancePatch patch, IPKConnection conn = null)
    {
        var query = patch.Apply(new Query("premium_allowances").Where("system", system));
        return await _db.QueryFirst<PremiumAllowance>(conn, query, "returning *");
    }

    public async Task<bool> UpdatePremiumAllowanceForIdChange(SystemId system, IPKConnection conn = null)
    {
        var query = new Query("premium_allowances")
        .AsUpdate(new
        {
            id_changes_remaining = new UnsafeLiteral("id_changes_remaining - 1")
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