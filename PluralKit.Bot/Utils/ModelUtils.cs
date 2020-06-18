using PluralKit.Core;

namespace PluralKit.Bot
{
    public static class ModelUtils
    {
        public static string NameFor(this PKMember member, Context ctx) =>
            member.NameFor(ctx.LookupContextFor(member));

        public static string DisplayName(this PKMember member) =>
            member.DisplayName ?? member.Name;
    }
}