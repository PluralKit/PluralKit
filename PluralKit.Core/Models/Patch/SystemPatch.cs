#nullable enable
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace PluralKit.Core
{
    public class SystemPatch: PatchObject
    {
        public Partial<string?> Name { get; set; }
        public Partial<string?> Hid { get; set; }
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
        public Partial<int?> MemberLimitOverride { get; set; }
        public Partial<int?> GroupLimitOverride { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("name", Name)
            .With("hid", Hid)
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
            .With("latch_timeout", LatchTimeout)
            .With("member_limit_override", MemberLimitOverride)
            .With("group_limit_override", GroupLimitOverride);

        public new void CheckIsValid()
        {
            if (AvatarUrl.Value != null && !MiscUtils.TryMatchUri(AvatarUrl.Value, out var avatarUri))
                throw new InvalidPatchException("avatar_url");
            if (BannerImage.Value != null && !MiscUtils.TryMatchUri(BannerImage.Value, out var bannerImage))
                throw new InvalidPatchException("banner");
            if (Color.Value != null && (!Regex.IsMatch(Color.Value, "^[0-9a-fA-F]{6}$")))
                throw new InvalidPatchException("color");
        }

        public static SystemPatch FromJSON(JObject o)
        {
            var patch = new SystemPatch();
            if (o.ContainsKey("name")) patch.Name = o.Value<string>("name").NullIfEmpty().BoundsCheckField(Limits.MaxSystemNameLength, "System name");
            if (o.ContainsKey("description")) patch.Description = o.Value<string>("description").NullIfEmpty().BoundsCheckField(Limits.MaxDescriptionLength, "System description");
            if (o.ContainsKey("tag")) patch.Tag = o.Value<string>("tag").NullIfEmpty().BoundsCheckField(Limits.MaxSystemTagLength, "System tag");
            if (o.ContainsKey("avatar_url")) patch.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty().BoundsCheckField(Limits.MaxUriLength, "System avatar URL");
            if (o.ContainsKey("banner")) patch.BannerImage = o.Value<string>("banner").NullIfEmpty().BoundsCheckField(Limits.MaxUriLength, "System banner URL");
            if (o.ContainsKey("timezone")) patch.UiTz = o.Value<string>("tz") ?? "UTC";

            // legacy: APIv1 uses "tz" instead of "timezone"
            // todo: remove in APIv2
            if (o.ContainsKey("tz")) patch.UiTz = o.Value<string>("tz") ?? "UTC";
            
            if (o.ContainsKey("description_privacy")) patch.DescriptionPrivacy = o.Value<string>("description_privacy").ParsePrivacy("description");
            if (o.ContainsKey("member_list_privacy")) patch.MemberListPrivacy = o.Value<string>("member_list_privacy").ParsePrivacy("member list");
            if (o.ContainsKey("front_privacy")) patch.FrontPrivacy = o.Value<string>("front_privacy").ParsePrivacy("front");
            if (o.ContainsKey("front_history_privacy")) patch.FrontHistoryPrivacy = o.Value<string>("front_history_privacy").ParsePrivacy("front history");
            return patch;
        }
    }
}