using Dapper.Contrib.Extensions;

using Newtonsoft.Json;

using NodaTime;

namespace PluralKit.Core {
    public class PKSystem
    {
        // Additions here should be mirrored in SystemStore::Save
        [Key] [JsonIgnore] public int Id { get; set; }
        [JsonProperty("id")] public string Hid { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("tag")] public string Tag { get; set; }
        [JsonProperty("avatar_url")] public string AvatarUrl { get; set; }
        [JsonIgnore] public string Token { get; set; }
        [JsonProperty("created")] public Instant Created { get; set; }
        [JsonProperty("tz")] public string UiTz { get; set; }
        public PrivacyLevel DescriptionPrivacy { get; set; }
        public PrivacyLevel MemberListPrivacy { get; set; }
        public PrivacyLevel FrontPrivacy { get; set; }
        public PrivacyLevel FrontHistoryPrivacy { get; set; }
        
        [JsonIgnore] public DateTimeZone Zone => DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz);
    }
}