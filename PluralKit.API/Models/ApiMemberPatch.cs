#nullable enable
using NodaTime;

using PluralKit.Core;

namespace PluralKit.API.Models
{
    public class ApiMemberPatch
    {
        public Partial<string> Name { get; set; }
        public Partial<string?> DisplayName { get; set; }
        public Partial<string?> Description { get; set; }
        public Partial<string?> Avatar { get; set; }
        public Partial<string?> Pronouns { get; set; }
        public Partial<string?> Color { get; set; }
        public Partial<LocalDate?> Birthday { get; set; }
        public Partial<ApiMemberPrivacyPatch> Privacy { get; set; }
    }

    public class ApiMemberPrivacyPatch
    {
        public Partial<PrivacyLevel> Visibility { get; set; }
        public Partial<PrivacyLevel> Name { get; set; }
        public Partial<PrivacyLevel> Description { get; set; }
        public Partial<PrivacyLevel> Avatar { get; set; }
        public Partial<PrivacyLevel> Birthday { get; set; }
        public Partial<PrivacyLevel> Pronouns { get; set; }
        public Partial<PrivacyLevel> Metadata { get; set; }
    }
}