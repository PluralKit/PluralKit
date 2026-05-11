using SqlKata;
using Npgsql;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task<PremiumAllowances?> GetSystemPremium(SystemId system)
        => _db.QueryFirst<PremiumAllowances?>(
            // meh
            @"select ps.status, ps.next_renewal_at,
                     coalesce(pa.id_changes_remaining, 0) as id_changes_remaining
              from premium_subscriptions ps
              left join premium_allowances pa on pa.subscription_id = ps.id
              where ps.system_id = @System and ps.status != 'canceled'",
            new { System = system.Value });

    public async Task<bool> UpdatePremiumAllowanceForIdChange(SystemId system, IPKConnection conn = null)
    {
        var query = new Query("premium_allowances")
        .AsUpdate(new
        {
            id_changes_remaining = new UnsafeLiteral("id_changes_remaining - 1")
        })
        .Where("system_id", system);

        try
        {
            await _db.ExecuteQuery(conn, query);
        }
        catch (PostgresException pe)
        {
            // this is checked upstream based on the return value of this func
            if (!pe.Message.Contains("violates check constraint"))
                throw;
            return false;
        }

        return true;
    }

    public async Task CreateHidChangelog(SystemId system, ulong discord_uid, string hid_type, string hid_old, string hid_new)
    {
        var query = new Query("hid_changelog").AsInsert(new
        {
            system,
            discord_uid,
            hid_type,
            hid_old,
            hid_new,
        });

        var changelogId = await _db.QueryFirst<int>(query, "returning id", messages: true);

        _logger.Information("Created HidChangelog {HidChangelogId} for system {SystemId}: {HidType} {OldHid} -> {NewHid}", changelogId, system, hid_type, hid_old, hid_new);
    }

    public Task<int> GetHidChangelogCountToday(SystemId system)
    {
        var query = new Query("hid_changelog")
            .SelectRaw("count(*)")
            .Where("system", system)
            .WhereDate("created", new UnsafeLiteral("cast(now() as date)"));

        return _db.QueryFirst<int>(query, messages: true);
    }
}

public class PremiumAllowances
{
    public string? Status { get; private set; }
    public string? NextRenewalAt { get; private set; }
    public int IdChangesRemaining { get; private set; }

    // todo(premium): maybe just is not "canceled"
    // todo(premiun): why does "past_due" count as active??
    public bool IsActive => Status is "active" or "past_due" or "canceling" or "lifetime";
    public bool IsCanceling => Status is "canceling";

    public bool Lifetime => Status is "lifetime";

    public static implicit operator bool(PremiumAllowances? allowances) => allowances?.IsActive ?? false;
}