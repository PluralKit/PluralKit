#nullable enable
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace PluralKit.Core
{
    public class GroupPatch: PatchObject
    {
        public Partial<string> Name { get; set; }
        public Partial<string> Hid { get; set; }
        public Partial<string?> DisplayName { get; set; }
        public Partial<string?> Description { get; set; }
        public Partial<string?> Icon { get; set; }
        public Partial<string?> BannerImage { get; set; }
        public Partial<string?> Color { get; set; }

        public Partial<PrivacyLevel> DescriptionPrivacy { get; set; }
        public Partial<PrivacyLevel> IconPrivacy { get; set; }
        public Partial<PrivacyLevel> ListPrivacy { get; set; }
        public Partial<PrivacyLevel> Visibility { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("name", Name)
            .With("hid", Hid)
            .With("display_name", DisplayName)
            .With("description", Description)
            .With("icon", Icon)
            .With("banner_image", BannerImage)
            .With("color", Color)
            .With("description_privacy", DescriptionPrivacy)
            .With("icon_privacy", IconPrivacy)
            .With("list_privacy", ListPrivacy)
            .With("visibility", Visibility);

        public new void AssertIsValid()
        {
            if (Icon.Value != null && !MiscUtils.TryMatchUri(Icon.Value, out var avatarUri))
                throw new ValidationError("icon");
            if (BannerImage.Value != null && !MiscUtils.TryMatchUri(BannerImage.Value, out var bannerImage))
                throw new ValidationError("banner");
            if (Color.Value != null && (!Regex.IsMatch(Color.Value, "^[0-9a-fA-F]{6}$")))
                throw new ValidationError("color");
        }
#nullable disable

        public static GroupPatch FromJson(JObject o)
        {
            var patch = new GroupPatch();

            if (o.ContainsKey("name") && o["name"].Type == JTokenType.Null)
                throw new ValidationError("Group name can not be set to null.");

            if (o.ContainsKey("name")) patch.Name = o.Value<string>("name");
            if (o.ContainsKey("display_name")) patch.DisplayName = o.Value<string>("display_name").NullIfEmpty();
            if (o.ContainsKey("description")) patch.Description = o.Value<string>("description").NullIfEmpty();
            if (o.ContainsKey("icon")) patch.Icon = o.Value<string>("icon").NullIfEmpty();
            if (o.ContainsKey("banner")) patch.BannerImage = o.Value<string>("banner").NullIfEmpty();
            if (o.ContainsKey("color")) patch.Color = o.Value<string>("color").NullIfEmpty()?.ToLower();

            if (o.ContainsKey("privacy") && o["privacy"].Type != JTokenType.Null)
            {
                var privacy = o.Value<JObject>("privacy");

                if (privacy.ContainsKey("description_privacy"))
                    patch.DescriptionPrivacy = privacy.ParsePrivacy("description_privacy");

                if (privacy.ContainsKey("icon_privacy"))
                    patch.IconPrivacy = privacy.ParsePrivacy("icon_privacy");

                if (privacy.ContainsKey("list_privacy"))
                    patch.ListPrivacy = privacy.ParsePrivacy("list_privacy");

                if (privacy.ContainsKey("visibility"))
                    patch.Visibility = privacy.ParsePrivacy("visibility");
            }

            return patch;
        }
    }
}