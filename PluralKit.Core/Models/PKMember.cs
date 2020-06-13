using System.Collections.Generic;

using Newtonsoft.Json;

using NodaTime;
using NodaTime.Text;

namespace PluralKit.Core {
    public class PKMember
    {
        public int Id { get; }
        public string Hid { get; set; }
        public int System { get; set; }
        public string Color { get; set; }
        public string AvatarUrl { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public LocalDate? Birthday { get; set; }
        public string Pronouns { get; set; }
        public string Description { get; set; }
        public ICollection<ProxyTag> ProxyTags { get; set; }
        public bool KeepProxy { get; set; }
        public Instant Created { get; }
        public int MessageCount { get; }

        public PrivacyLevel MemberPrivacy { get; set; }

        /// Returns a formatted string representing the member's birthday, taking into account that a year of "0001" or "0004" is hidden
        /// Before Feb 10 2020, the sentinel year was 0001, now it is 0004.
        [JsonIgnore] public string BirthdayString
        {
            get
            {
                if (Birthday == null) return null;

                var format = LocalDatePattern.CreateWithInvariantCulture("MMM dd, yyyy");
                if (Birthday?.Year == 1 || Birthday?.Year == 4) format = LocalDatePattern.CreateWithInvariantCulture("MMM dd");
                return format.Format(Birthday.Value);
            }
        }

        [JsonIgnore] public bool HasProxyTags => ProxyTags.Count > 0;
        public string ProxyName(string systemTag, string guildDisplayName)
        {
            if (systemTag == null) return guildDisplayName ?? DisplayName ?? Name;
            return $"{guildDisplayName ?? DisplayName ?? Name} {systemTag}";
        }
    }
    
    public struct ProxyTag
    {
        public ProxyTag(string prefix, string suffix)
        {
            // Normalize empty strings to null for DB
            Prefix = prefix?.Length == 0 ? null : prefix;
            Suffix = suffix?.Length == 0 ? null : suffix;
        }

        [JsonProperty("prefix")] public string Prefix { get; set; }
        [JsonProperty("suffix")] public string Suffix { get; set; }

        [JsonIgnore] public string ProxyString => $"{Prefix ?? ""}text{Suffix ?? ""}";

        public bool IsEmpty => Prefix == null && Suffix == null;

        public bool Equals(ProxyTag other) => Prefix == other.Prefix && Suffix == other.Suffix;

        public override bool Equals(object obj) => obj is ProxyTag other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Prefix != null ? Prefix.GetHashCode() : 0) * 397) ^
                       (Suffix != null ? Suffix.GetHashCode() : 0);
            }
        }
    }
}