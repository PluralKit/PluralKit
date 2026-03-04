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
}

public class PremiumAllowances
{
    public string? Status { get; private set; }
    public string? NextRenewalAt { get; private set; }
    public int IdChangesRemaining { get; private set; }

    public bool IsActive => Status is "active" or "past_due" or "canceling";
    public bool IsCanceling => Status is "canceling";
}