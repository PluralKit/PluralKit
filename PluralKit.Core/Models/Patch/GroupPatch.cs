#nullable enable
namespace PluralKit.Core
{
    public class GroupPatch: PatchObject
    {
        public Partial<string> Name { get; set; }
        public Partial<string?> Description { get; set; }
        public Partial<string?> Icon { get; set; }
        
        public Partial<PrivacyLevel> DescriptionPrivacy { get; set; }
        public Partial<PrivacyLevel> IconPrivacy { get; set; }
        public Partial<PrivacyLevel> Visibility { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("name", Name)
            .With("description", Description)
            .With("icon", Icon)
            .With("description_privacy", DescriptionPrivacy)
            .With("icon_privacy", IconPrivacy)
            .With("visibility", Visibility);
    }
}