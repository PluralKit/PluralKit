using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core;

public class PremiumAllowancePatch: PatchObject
{
    public Partial<int> IdChangesRemaining { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("id_changes_remaining", IdChangesRemaining)
    );
}