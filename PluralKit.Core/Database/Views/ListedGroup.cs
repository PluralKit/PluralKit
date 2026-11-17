namespace PluralKit.Core;

public class ListedGroup: PKGroup
{
    public IEnumerable<PKMember> Members { get; set; } = [];
    public ICollection<int>? MemberIds { get; private set; }

    public int MessageCount(LookupContext ctx, LookupContext cctx) =>
        Members.Where(m => m.MemberVisibility.CanAccess(cctx) && m.MetadataPrivacy.CanAccess(ctx))
                .Sum(m => m.MessageCount);

    public int MemberCount(LookupContext ctx) => Members.Count(m => m.MemberVisibility.CanAccess(ctx));
}