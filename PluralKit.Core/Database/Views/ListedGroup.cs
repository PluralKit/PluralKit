namespace PluralKit.Core;

public class ListedGroup: PKGroup
{
    public IEnumerable<PKMember> Members { get; set; } = [];
    public ICollection<int>? MemberIds { get; private set; }

    public int MessageCount(LookupContext ctx, PrivacyLevel? filter) =>
        Members.Where(m => (filter == null || m.MemberVisibility == filter) && m.MetadataPrivacy.CanAccess(ctx))
                .Sum(m => m.MessageCount);

    public int MemberCount(PrivacyLevel? filter) => Members.Count(m => filter == null || m.MemberVisibility == filter);
}