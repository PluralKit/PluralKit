using System;

using Dapper.Contrib.Extensions;

using Newtonsoft.Json;

using NodaTime;

namespace PluralKit.Core {
    public class PKSystem
    {
        // Additions here should be mirrored in SystemStore::Save
        [Key] public SystemId Id { get; }
        public Guid Uuid { get; }
        public string Hid { get; }
        public string Name { get; }
        public string Description { get; }
        public string Tag { get; }
        public string AvatarUrl { get; }
        public string Token { get; }
        public Instant Created { get; }
        public string UiTz { get; set; }
        public bool PingsEnabled { get; }
	    public PrivacyLevel DescriptionPrivacy { get; }
        public PrivacyLevel MemberListPrivacy { get;}
        public PrivacyLevel FrontPrivacy { get; }
        public PrivacyLevel FrontHistoryPrivacy { get; }
        public PrivacyLevel GroupListPrivacy { get; }
        public int? MemberLimitOverride { get; }
        public int? GroupLimitOverride { get; }
        
        [JsonIgnore] public DateTimeZone Zone => DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz);
    }

    public static class PKSystemExt
    {
        public static string DescriptionFor(this PKSystem system, LookupContext ctx) =>
            system.DescriptionPrivacy.Get(ctx, system.Description);
    }
}
