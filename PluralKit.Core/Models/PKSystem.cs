using Dapper.Contrib.Extensions;

using Newtonsoft.Json;

using NodaTime;

namespace PluralKit.Core {
    public class PKSystem
    {
        // Additions here should be mirrored in SystemStore::Save
        [Key] public SystemId Id { get; }
        public string Hid { get; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string TagSuffix { get; set; }
        public string TagPrefix { get; set; }
        public string AvatarUrl { get; set; }
        public string Token { get; set; }
        public Instant Created { get; }
        public string UiTz { get; set; }
        public bool PingsEnabled { get; set; }
	    public PrivacyLevel DescriptionPrivacy { get; set; }
        public PrivacyLevel MemberListPrivacy { get; set; }
        public PrivacyLevel FrontPrivacy { get; set; }
        public PrivacyLevel FrontHistoryPrivacy { get; set; }
        
        [JsonIgnore] public DateTimeZone Zone => DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz);
    }
}
