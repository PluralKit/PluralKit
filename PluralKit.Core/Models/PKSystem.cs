using Dapper.Contrib.Extensions;

using Newtonsoft.Json;

using NodaTime;

namespace PluralKit.Core {
    public class PKSystem
    {
        // Additions here should be mirrored in SystemStore::Save
        [Key] public int Id { get; set; }
        public string Hid { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Tag { get; set; }
        public string AvatarUrl { get; set; }
        public string Token { get; set; }
        public Instant Created { get; set; }
        public string UiTz { get; set; }
        public bool PingsEnabled { get; set; }
	    public PrivacyLevel DescriptionPrivacy { get; set; }
        public PrivacyLevel MemberListPrivacy { get; set; }
        public PrivacyLevel FrontPrivacy { get; set; }
        public PrivacyLevel FrontHistoryPrivacy { get; set; }
        
        [JsonIgnore] public DateTimeZone Zone => DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz);
    }
}
