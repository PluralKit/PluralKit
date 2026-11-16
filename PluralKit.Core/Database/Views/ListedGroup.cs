namespace PluralKit.Core;

public class ListedGroup: PKGroup
{
    public int PublicMemberCount { get; }

    public IEnumerable<PKMember> Members { get; set; } = [];
    public ICollection<int>? MemberIds { get; private set; }
}