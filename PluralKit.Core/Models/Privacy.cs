namespace PluralKit.Core
{
    public enum PrivacyLevel
    {
        Public = 1,
        Private = 2
    }

    public static class PrivacyExt
    {
        public static bool CanAccess(this PrivacyLevel level, LookupContext ctx) =>
            level == PrivacyLevel.Public || ctx == LookupContext.ByOwner;

        public static string Name(this PrivacyLevel level) => 
            level == PrivacyLevel.Public ? "public" : "private";
    }

    public enum LookupContext
    {
        ByOwner,
        ByNonOwner,
        API
    }
}