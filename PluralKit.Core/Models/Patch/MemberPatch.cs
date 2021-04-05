#nullable enable

using NodaTime;

namespace PluralKit.Core
{
    public class MemberPatch: PatchObject
    {
        public Partial<string> Name { get; set; }
        public Partial<string?> DisplayName { get; set; }
        public Partial<string?> AvatarUrl { get; set; }
        public Partial<string?> BannerImage { get; set; }
        public Partial<string?> Color { get; set; }
        public Partial<LocalDate?> Birthday { get; set; }
        public Partial<string?> Pronouns { get; set; }
        public Partial<string?> Description { get; set; }
        public Partial<ProxyTag[]> ProxyTags { get; set; }
        public Partial<bool> KeepProxy { get; set; }
        public Partial<int> MessageCount { get; set; }
        public Partial<bool> AllowAutoproxy { get; set; }
        public Partial<PrivacyLevel> Visibility { get; set; }
        public Partial<PrivacyLevel> NamePrivacy { get; set; }
        public Partial<PrivacyLevel> DescriptionPrivacy { get; set; }
        public Partial<PrivacyLevel> PronounPrivacy { get; set; }
        public Partial<PrivacyLevel> BirthdayPrivacy { get; set; }
        public Partial<PrivacyLevel> AvatarPrivacy { get; set; }
        public Partial<PrivacyLevel> MetadataPrivacy { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("name", Name)
            .With("display_name", DisplayName)
            .With("avatar_url", AvatarUrl)
            .With("banner_image", BannerImage)
            .With("color", Color)
            .With("birthday", Birthday)
            .With("pronouns", Pronouns)
            .With("description", Description)
            .With("proxy_tags", ProxyTags)
            .With("keep_proxy", KeepProxy)
            .With("message_count", MessageCount)
            .With("allow_autoproxy", AllowAutoproxy)
            .With("member_visibility", Visibility)
            .With("name_privacy", NamePrivacy)
            .With("description_privacy", DescriptionPrivacy)
            .With("pronoun_privacy", PronounPrivacy)
            .With("birthday_privacy", BirthdayPrivacy)
            .With("avatar_privacy", AvatarPrivacy)
            .With("metadata_privacy", MetadataPrivacy);
    }
}