#nullable enable
using System.Text.RegularExpressions;

namespace PluralKit.Core
{
    public class SystemPatch: PatchObject
    {
        public Partial<string?> Name { get; set; }
        public Partial<string?> Description { get; set; }
        public Partial<string?> Tag { get; set; }
        public Partial<string?> AvatarUrl { get; set; }
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

        public new void CheckIsValid()
        {
            if (AvatarUrl.Value != null && !MiscUtils.TryMatchUri(AvatarUrl.Value, out var avatarUri))
                throw new InvalidPatchException("avatar_url");
            if (Color.Value != null && (!Regex.IsMatch(Color.Value, "^[0-9a-fA-F]{6}$")))
                throw new InvalidPatchException("color");
        }

    }
}