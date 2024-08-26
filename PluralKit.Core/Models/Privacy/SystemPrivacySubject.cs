namespace PluralKit.Core;

public enum SystemPrivacySubject
{
    Name,
    Avatar,
    Description,
    Pronouns,
    MemberList,
    GroupList,
    Front,
    FrontHistory
}

public static class SystemPrivacyUtils
{
    public static SystemPatch WithPrivacy(this SystemPatch system, SystemPrivacySubject subject, PrivacyLevel level)
    {
        // what do you mean switch expressions can't be statements >.>
        _ = subject switch
        {
            SystemPrivacySubject.Name => system.NamePrivacy = level,
            SystemPrivacySubject.Avatar => system.AvatarPrivacy = level,
            SystemPrivacySubject.Description => system.DescriptionPrivacy = level,
            SystemPrivacySubject.Pronouns => system.PronounPrivacy = level,
            SystemPrivacySubject.Front => system.FrontPrivacy = level,
            SystemPrivacySubject.FrontHistory => system.FrontHistoryPrivacy = level,
            SystemPrivacySubject.MemberList => system.MemberListPrivacy = level,
            SystemPrivacySubject.GroupList => system.GroupListPrivacy = level,
            _ => throw new ArgumentOutOfRangeException($"Unknown privacy subject {subject}")
        };

        return system;
    }

    public static SystemPatch WithAllPrivacy(this SystemPatch system, PrivacyLevel level)
    {
        foreach (var subject in Enum.GetValues(typeof(SystemPrivacySubject)))
            WithPrivacy(system, (SystemPrivacySubject)subject, level);
        return system;
    }

    public static bool TryParseSystemPrivacy(string input, out SystemPrivacySubject subject)
    {
        switch (input.ToLowerInvariant())
        {
            case "name":
                subject = SystemPrivacySubject.Name;
                break;
            case "avatar":
            case "pfp":
            case "pic":
            case "icon":
                subject = SystemPrivacySubject.Avatar;
                break;
            case "description":
            case "desc":
            case "describe":
            case "d":
            case "bio":
            case "info":
            case "text":
            case "intro":
                subject = SystemPrivacySubject.Description;
                break;
            case "pronouns":
            case "pronoun":
            case "prns":
            case "pn":
                subject = SystemPrivacySubject.Pronouns;
                break;
            case "members":
            case "memberlist":
            case "list":
            case "mlist":
                subject = SystemPrivacySubject.MemberList;
                break;
            case "fronter":
            case "fronters":
            case "front":
                subject = SystemPrivacySubject.Front;
                break;
            case "switch":
            case "switches":
            case "fronthistory":
            case "fh":
                subject = SystemPrivacySubject.FrontHistory;
                break;
            case "groups":
            case "gs":
                subject = SystemPrivacySubject.GroupList;
                break;
            default:
                subject = default;
                return false;
        }

        return true;
    }
}