using System;
using System.Collections.Generic;

using Newtonsoft.Json;

using NodaTime;
using NodaTime.Text;

namespace PluralKit.Core {
    public class PKMember
    {
        // Dapper *can* figure out mapping to getter-only properties, but this doesn't work
        // when trying to map to *subclasses* (eg. ListedMember). Adding private setters makes it work anyway.
        public MemberId Id { get; private set; }
        public string Hid { get; private set; }
        public Guid Uuid { get; private set; }
        public SystemId System { get; private set; }
        public string Color { get; private set; }
        public string AvatarUrl { get; private set; }
        public string Name { get; private set; }
        public string DisplayName { get; private set; }
        public LocalDate? Birthday { get; private set; }
        public string Pronouns { get; private set; }
        public string Description { get; private set; }
        public ICollection<ProxyTag> ProxyTags { get; private set; }
        public bool KeepProxy { get; private set; }
        public Instant Created { get; private set; }
        public int MessageCount { get; private set; }

        public PrivacyLevel MemberVisibility { get; private set; }
        public PrivacyLevel DescriptionPrivacy { get; private set; }
        public PrivacyLevel AvatarPrivacy { get; private set; }
        public PrivacyLevel NamePrivacy { get; private set; } //ignore setting if no display name is set
        public PrivacyLevel BirthdayPrivacy { get; private set; }
        public PrivacyLevel PronounPrivacy { get; private set; }
        public PrivacyLevel MetadataPrivacy { get; private set; }
        // public PrivacyLevel ColorPrivacy { get; private set; }
        
        /// Returns a formatted string representing the member's birthday, taking into account that a year of "0001" or "0004" is hidden
        /// Before Feb 10 2020, the sentinel year was 0001, now it is 0004.
        [JsonIgnore] public string BirthdayString
        {
            get
            {
                if (Birthday == null) return null;

                var format = LocalDatePattern.CreateWithInvariantCulture("MMM dd, yyyy");
                if (Birthday?.Year == 1 || Birthday?.Year == 4) format = LocalDatePattern.CreateWithInvariantCulture("MMM dd");
                return format.Format(Birthday.Value);
            }
        }

        [JsonIgnore] public bool HasProxyTags => ProxyTags.Count > 0;
    }

    public static class PKMemberExt
    {
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
    }
}