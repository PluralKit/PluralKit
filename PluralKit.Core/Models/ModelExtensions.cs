namespace PluralKit.Core
{
    public static class ModelExtensions
    {
        public static string NameFor(this PKMember member, LookupContext ctx) => 
            member.NamePrivacy.CanAccess(ctx) ? member.Name : member.DisplayName ?? member.Name;

        public static string AvatarFor(this PKMember member, LookupContext ctx) =>
            member.AvatarPrivacy.CanAccess(ctx) ? member.AvatarUrl : null;
    }
}