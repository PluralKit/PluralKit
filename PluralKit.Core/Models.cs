using Dapper.Contrib.Extensions;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Text;

using PluralKit.Core;

namespace PluralKit
{
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
        [JsonIgnore] public DateTimeZone Zone => DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz);
    }

    public class PKMember
    {
        // Additions here should be mirrored in MemberStore::Save
        [JsonIgnore] public int Id { get; set; }
        [JsonProperty("id")] public string Hid { get; set; }
        [JsonIgnore] public int System { get; set; }
        [JsonProperty("color")] public string Color { get; set; }
        [JsonProperty("avatar_url")] public string AvatarUrl { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("display_name")] public string DisplayName { get; set; }
        [JsonProperty("birthday")] public LocalDate? Birthday { get; set; }
        [JsonProperty("pronouns")] public string Pronouns { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("prefix")] public string Prefix { get; set; }
        [JsonProperty("suffix")] public string Suffix { get; set; }
        [JsonProperty("created")] public Instant Created { get; set; }

        /// Returns a formatted string representing the member's birthday, taking into account that a year of "0001" is hidden
        [JsonIgnore] public string BirthdayString
        {
            get
            {
                if (Birthday == null) return null;

                var format = LocalDatePattern.CreateWithInvariantCulture("MMM dd, yyyy");
                if (Birthday?.Year == 1) format = LocalDatePattern.CreateWithInvariantCulture("MMM dd");
                return format.Format(Birthday.Value);
            }
        }

        [JsonIgnore] public bool HasProxyTags => Prefix != null || Suffix != null;
        [JsonIgnore] public string ProxyString => $"{Prefix ?? ""}text{Suffix ?? ""}";
        
        public string ProxyName(string systemTag)
        {
            if (systemTag == null) return DisplayName ?? Name;
            return $"{DisplayName ?? Name} {systemTag}";
        }
    }

    public class PKSwitch
    {
        public int Id { get; set; }
        public int System { get; set; }
        public Instant Timestamp { get; set; }
    }

    public class PKSwitchMember
    {
        public int Id { get; set; }
        public int Switch { get; set; }
        public int Member { get; set; }
    }
}