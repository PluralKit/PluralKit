using System;

using Newtonsoft.Json.Linq;

namespace PluralKit.Core
{
    public enum PrivacyLevel
    {
        Public = 1,
        Private = 2
    }

    public static class PrivacyLevelExt
    {
        public static bool CanAccess(this PrivacyLevel level, LookupContext ctx) =>
            level == PrivacyLevel.Public || ctx == LookupContext.ByOwner;

        public static string LevelName(this PrivacyLevel level) =>
            level == PrivacyLevel.Public ? "public" : "private";

        public static T Get<T>(this PrivacyLevel level, LookupContext ctx, T input, T fallback = default) =>
            level.CanAccess(ctx) ? input : fallback;

        public static string Explanation(this PrivacyLevel level) =>
            level switch
            {
                PrivacyLevel.Private => "**Private** (visible only when queried by you)",
                PrivacyLevel.Public => "**Public** (visible to everyone)",
                _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
            };

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

        public static string ToJsonString(this PrivacyLevel level) => level.LevelName();

        public static PrivacyLevel ParsePrivacy(this JObject o, string propertyName)
        {
            var input = o.Value<string>(propertyName);

            if (input == null) return PrivacyLevel.Public;
            if (input == "") return PrivacyLevel.Private;
            if (input == "private") return PrivacyLevel.Private;
            if (input == "public") return PrivacyLevel.Public;

            throw new ValidationError(propertyName);
        }

    }
}