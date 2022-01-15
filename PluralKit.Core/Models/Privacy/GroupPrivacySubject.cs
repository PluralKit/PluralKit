namespace PluralKit.Core;

public enum GroupPrivacySubject
{
    Name,
    Description,
    Icon,
    List,
    Metadata,
    Visibility
}

public static class GroupPrivacyUtils
{
    public static GroupPatch WithPrivacy(this GroupPatch group, GroupPrivacySubject subject, PrivacyLevel level)
    {
        // what do you mean switch expressions can't be statements >.>
        _ = subject switch
        {
            GroupPrivacySubject.Name => group.NamePrivacy = level,
            GroupPrivacySubject.Description => group.DescriptionPrivacy = level,
            GroupPrivacySubject.Icon => group.IconPrivacy = level,
            GroupPrivacySubject.List => group.ListPrivacy = level,
            GroupPrivacySubject.Metadata => group.MetadataPrivacy = level,
            GroupPrivacySubject.Visibility => group.Visibility = level,
            _ => throw new ArgumentOutOfRangeException($"Unknown privacy subject {subject}")
        };

        return group;
    }

    public static GroupPatch WithAllPrivacy(this GroupPatch member, PrivacyLevel level)
    {
        foreach (var subject in Enum.GetValues(typeof(GroupPrivacySubject)))
            member.WithPrivacy((GroupPrivacySubject)subject, level);
        return member;
    }

    public static bool TryParseGroupPrivacy(string input, out GroupPrivacySubject subject)
    {
        switch (input.ToLowerInvariant())
        {
            case "name":
                subject = GroupPrivacySubject.Name;
                break;
            case "description":
            case "desc":
            case "text":
            case "info":
                subject = GroupPrivacySubject.Description;
                break;
            case "avatar":
            case "pfp":
            case "pic":
            case "icon":
                subject = GroupPrivacySubject.Icon;
                break;
            case "visibility":
            case "hidden":
            case "shown":
            case "visible":
                subject = GroupPrivacySubject.Visibility;
                break;
            case "meta":
            case "metadata":
            case "created":
                subject = GroupPrivacySubject.Metadata;
                break;
            case "list":
            case "listing":
            case "members":
                subject = GroupPrivacySubject.List;
                break;
            default:
                subject = default;
                return false;
        }

        return true;
    }
}