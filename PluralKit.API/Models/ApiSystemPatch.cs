#nullable enable
using PluralKit.Core;

namespace PluralKit.API.Models
{
    public class ApiSystemPatch
    {
        public Partial<string?> Name { get; set; }
        public Partial<string?> Description { get; set; }
        public Partial<string?> Tag { get; set; }
        public Partial<string?> Icon { get; set; }
        public Partial<ApiSystemConfigPatch> Config { get; set; }
        public Partial<ApiSystemPrivacyPatch> Privacy { get; set; }
    }

    public class ApiSystemPrivacyPatch
    {
        public Partial<PrivacyLevel> Description { get; set; }
        public Partial<PrivacyLevel> MemberList { get; set; }
        public Partial<PrivacyLevel> GroupList { get; set; }
        public Partial<PrivacyLevel> LastSwitch { get; set; }
        public Partial<PrivacyLevel> SwitchHistory { get; set; }
    }

    public class ApiSystemConfigPatch
    {
        public Partial<string?> Timezone { get; set; }
        public Partial<bool> PingsEnabled { get; set; }
    }
}