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

        public static string LevelName(this PrivacyLevel level) => 
            level == PrivacyLevel.Public ? "public" : "private";

        public static T Get<T>(this PrivacyLevel level, LookupContext ctx, T input, T fallback = default) =>
            level.CanAccess(ctx) ? input : fallback;

        public static bool TryGet<T>(this PrivacyLevel level, LookupContext ctx, T input, out T output, T absentValue = default)
        {
            output = default;
            if (!level.CanAccess(ctx))
                return false;
            if (Equals(input, absentValue))
                return false;

            output = input;
            return true;
        }
    }

    public enum LookupContext
    {
        ByOwner,
        ByNonOwner,
        API
    }
}