using System.Text.RegularExpressions;

using Newtonsoft.Json;

using NodaTime;

#nullable enable
namespace PluralKit.Core
{
    public class PKGroup
    {
        [JsonIgnore] public GroupId Id { get; private set; }
        [JsonProperty("id")] public string Hid { get; private set; } = null!;
        [JsonIgnore] public SystemId System { get; private set; }

        [JsonProperty("name")] public string Name { get; private set; } = null!;
        [JsonProperty("display_name")] public string? DisplayName { get; private set; }
        [JsonProperty("description")] public string? Description { get; private set; }
        [JsonProperty("icon")] public string? Icon { get; private set; }
        [JsonProperty("color")] public string? Color { get; private set; }

        [JsonProperty("description_privacy")] public PrivacyLevel DescriptionPrivacy { get; private set; }
        [JsonProperty("icon_privacy")] public PrivacyLevel IconPrivacy { get; private set; }
        [JsonProperty("list_privacy")] public PrivacyLevel ListPrivacy { get; private set; }
        [JsonProperty("visibility")] public PrivacyLevel Visibility { get; private set; }
        
        public Instant Created { get; private set; }

        public bool Valid =>
            Name != null &&
            !Name.IsLongerThan(Limits.MaxGroupNameLength) &&
            !DisplayName.IsLongerThan(Limits.MaxGroupNameLength) &&
            !Description.IsLongerThan(Limits.MaxDescriptionLength) &&
            (Color == null || Regex.IsMatch(Color, "[0-9a-fA-F]{6}")) &&

            // sanity checks
            !Icon.IsLongerThan(1000);

        public GroupPatch ToGroupPatch() => new GroupPatch
        {
            Name = Name,
            DisplayName = DisplayName,
            Description = Description,
            Icon = Icon,
            Color = Color,
            DescriptionPrivacy = DescriptionPrivacy,
            IconPrivacy = IconPrivacy,
            ListPrivacy = ListPrivacy,
            Visibility = Visibility,
        };
    }

    public static class PKGroupExt
    {
        public static string? DescriptionFor(this PKGroup group, LookupContext ctx) =>
            group.DescriptionPrivacy.Get(ctx, group.Description);
        
        public static string? IconFor(this PKGroup group, LookupContext ctx) =>
            group.IconPrivacy.Get(ctx, group.Icon);
    }
}