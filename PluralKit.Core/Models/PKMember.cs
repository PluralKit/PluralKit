using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

using NodaTime;
using NodaTime.Text;

namespace PluralKit.Core {
    public class PKMember
    {
        // Dapper *can* figure out mapping to getter-only properties, but this doesn't work
        // when trying to map to *subclasses* (eg. ListedMember). Adding private setters makes it work anyway.
        
        // though apparently we're setting stuff to public for DataFileService so that's not an issue anymore
        [JsonIgnore] public MemberId Id { get; set; }
        [JsonProperty("id")] public string Hid { get; set; }
        [JsonIgnore] public SystemId System { get; set; }
        [JsonProperty("color")] public string Color { get; set; }
        [JsonProperty("avatar_url")] public string AvatarUrl { get; set; }
        [JsonProperty("name")] public string Name { get; set; } 
        [JsonProperty("display_name")] public string DisplayName { get; set; }
        [JsonProperty("birthday")] public LocalDate? Birthday { get; set; }
        [JsonProperty("pronouns")] public string Pronouns { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("proxy_tags")] public ICollection<ProxyTag> ProxyTags { get; set; }
        [JsonProperty("keep_proxy")] public bool KeepProxy { get; set; }
        [JsonProperty("created")] public Instant Created { get; set; }
        [JsonProperty("message_count")] public int MessageCount { get; set; }
        [JsonProperty("allow_autoproxy")] public bool AllowAutoproxy { get; set; }

        [JsonProperty("visibility")] public PrivacyLevel MemberVisibility { get; set; }
        [JsonProperty("description_privacy")] public PrivacyLevel DescriptionPrivacy { get; set; }
        [JsonProperty("avatar_privacy")] public PrivacyLevel AvatarPrivacy { get; set; }
        [JsonProperty("name_privacy")] public PrivacyLevel NamePrivacy { get; set; } //ignore setting if no display name is set
        [JsonProperty("birthday_privacy")] public PrivacyLevel BirthdayPrivacy { get; set; }
        [JsonProperty("pronoun_privacy")] public PrivacyLevel PronounPrivacy { get; set; }
        [JsonProperty("metadata_privacy")] public PrivacyLevel MetadataPrivacy { get; set; }
        // public PrivacyLevel ColorPrivacy { get; private set; }
        
        // legacy, for old exports
        [JsonProperty("prefix")] [JsonIgnore] private string Prefix { get; set; }
        [JsonProperty("suffix")] [JsonIgnore] private string Suffix { get; set; }
        
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
        
        [JsonIgnore] public bool Valid =>
            Name != null &&
            !Name.IsLongerThan(Limits.MaxMemberNameLength) &&
            !DisplayName.IsLongerThan(Limits.MaxMemberNameLength) &&
            !Description.IsLongerThan(Limits.MaxDescriptionLength) &&
            !Pronouns.IsLongerThan(Limits.MaxPronounsLength) &&
            (Color == null || Regex.IsMatch(Color, "[0-9a-fA-F]{6}")) &&
            // (Birthday == null || DateTimeFormats.DateExportFormat.Parse(Birthday).Success) &&

            // Sanity checks
            !AvatarUrl.IsLongerThan(1000) &&
            (ProxyTags == null || ProxyTags.Count < 100);

        public MemberPatch ToMemberPatch() => new MemberPatch
        {
            Name = Name,
            DisplayName = DisplayName,
            AvatarUrl = AvatarUrl,
            Color = Color,
            Birthday = Birthday,
            Pronouns = Pronouns,
            Description = Description,
            ProxyTags = (Prefix != null || Suffix != null)
                ? new[] {new ProxyTag(Prefix, Suffix)}
                : (ProxyTags ?? new ProxyTag[] { }).Where(tag => !tag.IsEmpty).ToArray(),
            KeepProxy = KeepProxy,
            MessageCount = MessageCount,
            AllowAutoproxy = AllowAutoproxy,
            Visibility = MemberVisibility,
            NamePrivacy = NamePrivacy,
            DescriptionPrivacy = DescriptionPrivacy,
            PronounPrivacy = PronounPrivacy,
            BirthdayPrivacy = BirthdayPrivacy,
            AvatarPrivacy = AvatarPrivacy,
            MetadataPrivacy = MetadataPrivacy,
        };
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