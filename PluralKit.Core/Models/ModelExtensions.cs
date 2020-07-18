using NodaTime;

namespace PluralKit.Core
{
    public static class ModelExtensions
    {
        public static string DescriptionFor(this PKSystem system, LookupContext ctx) =>
            system.DescriptionPrivacy.Get(ctx, system.Description);

        public static string NameFor(this PKMember member, LookupContext ctx) =>
            member.NamePrivacy.Get(ctx, member.Name, member.DisplayName ?? member.Name);

        public static string AvatarFor(this PKMember member, LookupContext ctx) =>
            member.AvatarPrivacy.Get(ctx, member.AvatarUrl);

        public static string DescriptionFor(this PKMember member, LookupContext ctx) =>
            member.DescriptionPrivacy.Get(ctx, member.Description);

        public static LocalDate? BirthdayFor(this PKMember member, LookupContext ctx) =>
            member.BirthdayPrivacy.Get(ctx, member.Birthday);

        public static string PronounsFor(this PKMember member, LookupContext ctx) =>
            member.PronounPrivacy.Get(ctx, member.Pronouns);

        public static Instant? CreatedFor(this PKMember member, LookupContext ctx) =>
            member.MetadataPrivacy.Get(ctx, (Instant?) member.Created);

        public static int MessageCountFor(this PKMember member, LookupContext ctx) =>
            member.MetadataPrivacy.Get(ctx, member.MessageCount);

        public static string DescriptionFor(this PKGroup group, LookupContext ctx) =>
            group.DescriptionPrivacy.Get(ctx, group.Description);
        
        public static string IconFor(this PKGroup group, LookupContext ctx) =>
            group.IconPrivacy.Get(ctx, group.Icon);
    }
}