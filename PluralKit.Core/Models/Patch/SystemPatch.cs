#nullable enable
namespace PluralKit.Core
{
    public class SystemPatch: PatchObject
    {
        public Partial<string?> Name { get; set; }
        public Partial<string?> Description { get; set; }
        public Partial<string?> Tag { get; set; }
        public Partial<string?> AvatarUrl { get; set; }
        public Partial<string?> BannerImage { get; set; }
        public Partial<string?> Color { get; set; }
        public Partial<string?> Token { get; set; }
        public Partial<string> UiTz { get; set; }
        public Partial<PrivacyLevel> DescriptionPrivacy { get; set; }
        public Partial<PrivacyLevel> MemberListPrivacy { get; set; }
        public Partial<PrivacyLevel> GroupListPrivacy { get; set; }
        public Partial<PrivacyLevel> FrontPrivacy { get; set; }
        public Partial<PrivacyLevel> FrontHistoryPrivacy { get; set; }
        public Partial<bool> PingsEnabled { get; set; }
        public Partial<int?> LatchTimeout { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("name", Name)
            .With("description", Description)
            .With("tag", Tag)
            .With("avatar_url", AvatarUrl)
            .With("banner_image", BannerImage)
            .With("color", Color)
            .With("token", Token)
            .With("ui_tz", UiTz)
            .With("description_privacy", DescriptionPrivacy)
            .With("member_list_privacy", MemberListPrivacy)
            .With("group_list_privacy", GroupListPrivacy)
            .With("front_privacy", FrontPrivacy)
            .With("front_history_privacy", FrontHistoryPrivacy)
            .With("pings_enabled", PingsEnabled)
            .With("latch_timeout", LatchTimeout);
    }
}