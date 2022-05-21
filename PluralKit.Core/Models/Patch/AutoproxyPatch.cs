using NodaTime;

using SqlKata;

namespace PluralKit.Core;

public class AutoproxyPatch: PatchObject
{
    public Partial<AutoproxyMode> AutoproxyMode { get; set; }
    public Partial<MemberId?> AutoproxyMember { get; set; }

    public Partial<Instant> LastLatchTimestamp { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("autoproxy_mode", AutoproxyMode)
        .With("autoproxy_member", AutoproxyMember)
        .With("last_latch_timestamp", LastLatchTimestamp)
    );
}