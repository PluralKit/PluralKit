#nullable enable
using System;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API.Models
{
    public class ApiSystem
    {
        public Guid SystemId { get; set; }
        public string ShortId { get; set; } = null!;
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Tag { get; set; }
        public string? Icon { get; set; }
        public ApiSystemConfig Config { get; set; } = null!;
        public ApiSystemPrivacy? Privacy { get; set; }
        public Instant Created { get; set; }
    }

    public class ApiSystemConfig
    {
        public string? Timezone { get; set; }
        public bool PingsEnabled { get; set; }
    }

    public class ApiSystemPrivacy
    {
        public PrivacyLevel Description { get; set; }
        public PrivacyLevel MemberList { get; set; }
        public PrivacyLevel GroupList { get; set; }
        public PrivacyLevel LastSwitch { get; set; }
        public PrivacyLevel SwitchHistory { get; set; }
    }
}