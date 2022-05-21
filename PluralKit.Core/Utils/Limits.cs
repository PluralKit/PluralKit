namespace PluralKit.Core;

public static class Limits
{
    public static readonly int MaxProxyNameLength = 80;

    public static readonly int MaxSystemNameLength = 100;
    public static readonly int MaxSystemTagLength = MaxProxyNameLength - 1;
    public static readonly int MaxMemberCount = 1000;
    public static readonly int MaxGroupCount = 250;
    public static int WarnThreshold(int limit) => limit - 50;

    public static readonly int MaxDescriptionLength = 1000;
    public static readonly int MaxProxyTagLength = 100;
    public static readonly int MaxSwitchMemberCount = 150;
    public static readonly int MaxMemberNameLength = 100; // Fair bit larger than MaxProxyNameLength for bookkeeping
    public static readonly int MaxGroupNameLength = 100;
    public static readonly int MaxPronounsLength = 100;

    // May need to be set higher, I know there are URLs longer than this in prod (they can rehost, I guess...)
    public static readonly int MaxUriLength = 256;

    public static readonly long AvatarFileSizeLimit = 1024 * 1024;
    public static readonly int AvatarDimensionLimit = 1000;
}