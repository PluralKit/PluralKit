namespace PluralKit.Core
{
    public static class ModelExtensions
    {
        public static string NameFor(this PKMember member, LookupContext ctx) => 
            member.NamePrivacy.CanAccess(ctx) ? member.Name : member.DisplayName ?? member.Name;
    }
}