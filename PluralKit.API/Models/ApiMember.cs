#nullable enable
using System;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API.Models
{
    public class ApiMember
    {
        public Guid MemberId { get; set; }
        // public Guid SystemId { get; set; }
        public string ShortId { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? Description { get; set; }
        public string? Avatar { get; set; }
        public string? Pronouns { get; set; }
        public string? Color { get; set; }
        public LocalDate? Birthday { get; set; }
        public int? MessageCount { get; set; }
        public ApiMemberPrivacy? Privacy { get; set; }
        public Instant? Created { get; set; }
    }

    public class ApiMemberPrivacy
    {
        public PrivacyLevel Visibility { get; set; }
        public PrivacyLevel Name { get; set; }
        public PrivacyLevel Description { get; set; }
        public PrivacyLevel Avatar { get; set; }
        public PrivacyLevel Birthday { get; set; }
        public PrivacyLevel Pronouns { get; set; }
        public PrivacyLevel Metadata { get; set; }
    }
}