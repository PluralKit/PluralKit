#nullable enable
using System;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

using NodaTime;

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

        public new void AssertIsValid()
        {
            if (Name.Value != null)
                AssertValid(Name.Value, "name", Limits.MaxSystemNameLength);
            if (Description.Value != null)
                AssertValid(Description.Value, "description", Limits.MaxDescriptionLength);
            if (Tag.Value != null)
                AssertValid(Tag.Value, "tag", Limits.MaxSystemTagLength);
            if (AvatarUrl.Value != null)
                AssertValid(AvatarUrl.Value, "avatar_url", Limits.MaxUriLength,
                    s => MiscUtils.TryMatchUri(s, out var avatarUri));
            if (BannerImage.Value != null)
                AssertValid(BannerImage.Value, "banner", Limits.MaxUriLength,
                    s => MiscUtils.TryMatchUri(s, out var bannerUri));
            if (Color.Value != null)
                AssertValid(Color.Value, "color", "^[0-9a-fA-F]{6}$");
            if (UiTz.IsPresent && DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz.Value) == null)
                throw new ValidationError("avatar_url");
        }

        public static SystemPatch FromJSON(JObject o)
        {
            var patch = new SystemPatch();
            if (o.ContainsKey("name")) patch.Name = o.Value<string>("name").NullIfEmpty();
            if (o.ContainsKey("description")) patch.Description = o.Value<string>("description").NullIfEmpty();
            if (o.ContainsKey("tag")) patch.Tag = o.Value<string>("tag").NullIfEmpty();
            if (o.ContainsKey("avatar_url")) patch.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty();
            if (o.ContainsKey("banner")) patch.BannerImage = o.Value<string>("banner").NullIfEmpty();
            if (o.ContainsKey("timezone")) patch.UiTz = o.Value<string>("tz") ?? "UTC";

            // legacy: APIv1 uses "tz" instead of "timezone"
            // todo: remove in APIv2
            if (o.ContainsKey("tz")) patch.UiTz = o.Value<string>("tz") ?? "UTC";
            
            if (o.ContainsKey("description_privacy")) patch.DescriptionPrivacy = o.ParsePrivacy("description_privacy");
            if (o.ContainsKey("member_list_privacy")) patch.MemberListPrivacy = o.ParsePrivacy("member_list_privacy");
            if (o.ContainsKey("front_privacy")) patch.FrontPrivacy = o.ParsePrivacy("front_privacy");
            if (o.ContainsKey("front_history_privacy")) patch.FrontHistoryPrivacy = o.ParsePrivacy("front_history_privacy");
            return patch;
        }
    }
}