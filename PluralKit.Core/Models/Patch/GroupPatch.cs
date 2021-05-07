#nullable enable
using System.Text.RegularExpressions;

namespace PluralKit.Core
{
    public class GroupPatch: PatchObject
    {
        public Partial<string> Name { get; set; }
        public Partial<string?> DisplayName { get; set; }
        public Partial<string?> Description { get; set; }
        public Partial<string?> Icon { get; set; }
        public Partial<string?> Color { get; set; }
        
        public Partial<PrivacyLevel> DescriptionPrivacy { get; set; }
        public Partial<PrivacyLevel> IconPrivacy { get; set; }
        public Partial<PrivacyLevel> ListPrivacy { get; set; }
        public Partial<PrivacyLevel> Visibility { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("name", Name)
            .With("display_name", DisplayName)
            .With("description", Description)
            .With("icon", Icon)
            .With("color", Color)
            .With("description_privacy", DescriptionPrivacy)
            .With("icon_privacy", IconPrivacy)
            .With("list_privacy", ListPrivacy)
            .With("visibility", Visibility);

        public new void CheckIsValid()
        {
            if (Icon.Value != null && !MiscUtils.TryMatchUri(Icon.Value, out var avatarUri))
                throw new InvalidPatchException("avatar_url");
            if (Color.Value != null && (!Regex.IsMatch(Color.Value, "^[0-9a-fA-F]{6}$")))
                throw new InvalidPatchException("color");
        }

    }
}