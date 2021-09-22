#nullable enable
using System.Linq;
using System.Text.RegularExpressions;

using NodaTime;

using Newtonsoft.Json.Linq;

namespace PluralKit.Core
{
    public class MemberPatch: PatchObject
    {
        public Partial<string> Name { get; set; }
        public Partial<string> Hid { get; set; }
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
            .With("hid", Hid)
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

        public new void AssertIsValid()
        {
            if (Name.IsPresent)
                AssertValid(Name.Value, "name", Limits.MaxMemberNameLength);
            if (DisplayName.Value != null)
                AssertValid(DisplayName.Value, "display_name", Limits.MaxMemberNameLength);
            if (AvatarUrl.Value != null)
                AssertValid(AvatarUrl.Value, "avatar_url", Limits.MaxUriLength,
                    s => MiscUtils.TryMatchUri(s, out var avatarUri));
            if (BannerImage.Value != null)
                AssertValid(BannerImage.Value, "banner", Limits.MaxUriLength,
                    s => MiscUtils.TryMatchUri(s, out var bannerUri));
            if (Color.Value != null)
                AssertValid(Color.Value, "color", "^[0-9a-fA-F]{6}$");
            if (Pronouns.Value != null)
                AssertValid(Pronouns.Value, "pronouns", Limits.MaxPronounsLength);
            if (Description.Value != null)
                AssertValid(Description.Value, "description", Limits.MaxDescriptionLength);
            if (ProxyTags.IsPresent && (ProxyTags.Value.Length > 100 ||
                                        ProxyTags.Value.Any(tag => tag.ProxyString.IsLongerThan(100))))
                // todo: have a better error for this
                throw new ValidationError("proxy_tags");
        }

#nullable disable

        public static MemberPatch FromJSON(JObject o)
        {
            var patch = new MemberPatch();

            if (o.ContainsKey("name") && o["name"].Type == JTokenType.Null)
                throw new ValidationError("Member name can not be set to null.");

            if (o.ContainsKey("name")) patch.Name = o.Value<string>("name");
            if (o.ContainsKey("color")) patch.Color = o.Value<string>("color").NullIfEmpty()?.ToLower();
            if (o.ContainsKey("display_name")) patch.DisplayName = o.Value<string>("display_name").NullIfEmpty();
            if (o.ContainsKey("avatar_url")) patch.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty();
            if (o.ContainsKey("banner")) patch.BannerImage = o.Value<string>("banner").NullIfEmpty();

            if (o.ContainsKey("birthday"))
            {
                var str = o.Value<string>("birthday").NullIfEmpty();
                var res = DateTimeFormats.DateExportFormat.Parse(str);
                if (res.Success) patch.Birthday = res.Value;
                else if (str == null) patch.Birthday = null;
                else throw new ValidationError("birthday");
            }

            if (o.ContainsKey("pronouns")) patch.Pronouns = o.Value<string>("pronouns").NullIfEmpty();
            if (o.ContainsKey("description")) patch.Description = o.Value<string>("description").NullIfEmpty();
            if (o.ContainsKey("keep_proxy")) patch.KeepProxy = o.Value<bool>("keep_proxy");

            // legacy: used in old export files and APIv1
            if (o.ContainsKey("prefix") || o.ContainsKey("suffix") && !o.ContainsKey("proxy_tags"))
                patch.ProxyTags = new[] { new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix")) };
            else if (o.ContainsKey("proxy_tags"))
                patch.ProxyTags = o.Value<JArray>("proxy_tags")
                    .OfType<JObject>().Select(o => new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix")))
                    .Where(p => p.Valid)
                    .ToArray();

            if (o.ContainsKey("privacy")) //TODO: Deprecate this completely in api v2
            {
                var plevel = o.ParsePrivacy("privacy");

                patch.Visibility = plevel;
                patch.NamePrivacy = plevel;
                patch.AvatarPrivacy = plevel;
                patch.DescriptionPrivacy = plevel;
                patch.BirthdayPrivacy = plevel;
                patch.PronounPrivacy = plevel;
                // member.ColorPrivacy = plevel;
                patch.MetadataPrivacy = plevel;
            }
            else
            {
                if (o.ContainsKey("visibility")) patch.Visibility = o.ParsePrivacy("visibility");
                if (o.ContainsKey("name_privacy")) patch.NamePrivacy = o.ParsePrivacy("name_privacy");
                if (o.ContainsKey("description_privacy")) patch.DescriptionPrivacy = o.ParsePrivacy("description_privacy");
                if (o.ContainsKey("avatar_privacy")) patch.AvatarPrivacy = o.ParsePrivacy("avatar_privacy");
                if (o.ContainsKey("birthday_privacy")) patch.BirthdayPrivacy = o.ParsePrivacy("birthday_privacy");
                if (o.ContainsKey("pronoun_privacy")) patch.PronounPrivacy = o.ParsePrivacy("pronoun_privacy");
                // if (o.ContainsKey("color_privacy")) member.ColorPrivacy = o.ParsePrivacy("member");
                if (o.ContainsKey("metadata_privacy")) patch.MetadataPrivacy = o.ParsePrivacy("metadata_privacy");
            }

            return patch;
        }
    }
}