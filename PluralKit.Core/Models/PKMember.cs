using System.Collections.Generic;

using Newtonsoft.Json;

using NodaTime;
using NodaTime.Text;

namespace PluralKit.Core {
    public class PKMember
    {
        public MemberId Id { get; }
        public string Hid { get; set; }
        public SystemId System { get; set; }
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

        public PrivacyLevel MemberVisibility { get; set; }
        public PrivacyLevel DescriptionPrivacy { get; set; }
        public PrivacyLevel NamePrivacy { get; set; } //ignore setting if no display name is set
        public PrivacyLevel BirthdayPrivacy { get; set; }
        public PrivacyLevel PronounPrivacy { get; set; }
        public PrivacyLevel MessageCountPrivacy { get; set; }
        public PrivacyLevel CreatedTimestampPrivacy { get; set; }
        public PrivacyLevel ColorPrivacy { get; set; }

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