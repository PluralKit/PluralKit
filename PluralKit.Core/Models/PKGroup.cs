using NodaTime;

#nullable enable
namespace PluralKit.Core
{
    public class PKGroup
    {
        public GroupId Id { get; private set; }
        public string Hid { get; private set; } = null!;
        public SystemId System { get; private set; }

        public string Name { get; private set; } = null!;
        public string? DisplayName { get; private set; }
        public string? Description { get; private set; }
        public string? Icon { get; private set; }
        public string? BannerImage { get; private set; }
        public string? Color { get; private set; }

        public PrivacyLevel DescriptionPrivacy { get; private set; }
        public PrivacyLevel IconPrivacy { get; private set; }
        public PrivacyLevel ListPrivacy { get; private set; }
        public PrivacyLevel Visibility { get; private set; }
        
        public Instant Created { get; private set; }
    }

    public static class PKGroupExt
    {
        public static string? DescriptionFor(this PKGroup group, LookupContext ctx) =>
            group.DescriptionPrivacy.Get(ctx, group.Description);
        
        public static string? IconFor(this PKGroup group, LookupContext ctx) =>
            group.IconPrivacy.Get(ctx, group.Icon);
    }
}