#nullable enable
using System;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API.Models
{
    public class ApiGroup
    {
        public Guid GroupId { get; set; }
        public string ShortId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public ApiGroupPrivacy? Privacy { get; set; }
        public Instant? Created { get; set; }
    }
    
    public class ApiGroupPrivacy
    {
        public PrivacyLevel Visibility { get; set; }
        public PrivacyLevel Description { get; set; }
        public PrivacyLevel List { get; set; }
        public PrivacyLevel Icon { get; set; }
    }
}