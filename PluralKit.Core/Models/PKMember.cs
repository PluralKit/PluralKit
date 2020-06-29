using System.Collections.Generic;

using Newtonsoft.Json;

using NodaTime;
using NodaTime.Text;

namespace PluralKit.Core {
    public class PKMember
    {
        public MemberId Id { get; }
        public string Hid { get; }
        public SystemId System { get; }
        public string Color { get; }
        public string AvatarUrl { get; }
        public string Name { get; }
        public string DisplayName { get; }
        public LocalDate? Birthday { get; }
        public string Pronouns { get; }
        public string Description { get; }
        public ICollection<ProxyTag> ProxyTags { get; }
        public bool KeepProxy { get; }
        public Instant Created { get; }
        public int MessageCount { get; }

        public PrivacyLevel MemberVisibility { get; }
        public PrivacyLevel DescriptionPrivacy { get; }
        public PrivacyLevel AvatarPrivacy { get; }
        public PrivacyLevel NamePrivacy { get; } //ignore setting if no display name is set
        public PrivacyLevel BirthdayPrivacy { get; }
        public PrivacyLevel PronounPrivacy { get; }
        public PrivacyLevel MetadataPrivacy { get; }
        // public PrivacyLevel ColorPrivacy { get; set; }
        
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
    }
}