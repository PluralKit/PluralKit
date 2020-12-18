#nullable enable
using PluralKit.Core;

namespace PluralKit.API.Models
{
    public class ApiGroupPatch
    {
        public Partial<string> Name { get; set; }
        public Partial<string?> DisplayName { get; set; }
        public Partial<string?> Description { get; set; }
        public Partial<string?> Icon { get; set; }
        public Partial<ApiGroupPrivacyPatch> Privacy { get; set; }
    }

    public class ApiGroupPrivacyPatch
    {
        public Partial<PrivacyLevel> Visibility { get; set; }
        public Partial<PrivacyLevel> Description { get; set; }
        public Partial<PrivacyLevel> List { get; set; }
        public Partial<PrivacyLevel> Icon { get; set; }
    }
}