#nullable enable
using System;
using System.Text.RegularExpressions;

namespace PluralKit.Core.Validation
{
    public class ModelValidator
    {
        private static readonly Regex ColorRegex = new Regex("^[0-9a-fA-F]{6}$");

        public static void ValidateSystem(SystemPatch patch)
        {
            if (patch.Name.IsPresent && patch.Name.Value.IsLongerThan(Limits.MaxSystemNameLength))
                throw new ModelValidationException(nameof(patch.Name),
                    $"System name is too long ({patch.Name.Value?.Length} > {Limits.MaxSystemNameLength} chars)");
            
            if (patch.Description.IsPresent && patch.Description.Value.IsLongerThan(Limits.MaxDescriptionLength))
                throw new ModelValidationException(nameof(patch.Description),
                    $"System description is too long ({patch.Description.Value?.Length} > {Limits.MaxDescriptionLength} chars)");

            if (patch.AvatarUrl.IsPresent && patch.AvatarUrl.Value.IsLongerThan(Limits.MaxUriLength))
                throw new ModelValidationException(nameof(patch.AvatarUrl),
                    $"System icon URL is too long ({patch.AvatarUrl.Value?.Length} > {Limits.MaxUriLength} chars)");
            
            if (patch.Tag.IsPresent && patch.Tag.Value.IsLongerThan(Limits.MaxSystemTagLength))
                throw new ModelValidationException(nameof(patch.AvatarUrl),
                    $"System tag is too long ({patch.Tag.Value?.Length} > {Limits.MaxSystemTagLength} chars)");
        }
        
        public static void ValidateMember(MemberPatch patch)
        {
            if (patch.Name.IsPresent && string.IsNullOrWhiteSpace(patch.Name.Value))
                throw new ModelValidationException(nameof(patch.Name), "Member name may not be empty or null");
            
            if (patch.Name.IsPresent && patch.Name.Value.IsLongerThan(Limits.MaxMemberNameLength))
                throw new ModelValidationException(nameof(patch.Name),
                    $"Member name is too long ({patch.Name.Value?.Length} > {Limits.MaxMemberNameLength} chars)");

            if (patch.DisplayName.IsPresent && patch.DisplayName.Value.IsLongerThan(Limits.MaxMemberNameLength))
                throw new ModelValidationException(nameof(patch.DisplayName),
                    $"Member display name is too long ({patch.DisplayName.Value?.Length} > {Limits.MaxMemberNameLength} chars)");

            if (patch.Description.IsPresent && patch.Description.Value.IsLongerThan(Limits.MaxDescriptionLength))
                throw new ModelValidationException(nameof(patch.Description),
                    $"Member description is too long ({patch.Description.Value?.Length} > {Limits.MaxDescriptionLength} chars)");

            if (patch.Pronouns.IsPresent && patch.Pronouns.Value.IsLongerThan(Limits.MaxPronounsLength))
                throw new ModelValidationException(nameof(patch.Pronouns),
                    $"Member pronouns are too long ({patch.Pronouns.Value?.Length} > {Limits.MaxPronounsLength} chars)");

            if (patch.AvatarUrl.IsPresent && patch.AvatarUrl.Value.IsLongerThan(Limits.MaxUriLength))
                throw new ModelValidationException(nameof(patch.AvatarUrl),
                    $"Member avatar URL is too long ({patch.AvatarUrl.Value?.Length} > {Limits.MaxUriLength} chars)");

            if (patch.AvatarUrl.IsPresent && patch.AvatarUrl.Value != null &&
                !Uri.TryCreate(patch.AvatarUrl.Value, UriKind.Absolute, out _))
                throw new ModelValidationException(nameof(patch.AvatarUrl), "Member avatar URL is not a valid URL");

            if (patch.Color.IsPresent && patch.Color.Value != null && ColorRegex.IsMatch(patch.Color.Value ?? ""))
                throw new ModelValidationException(nameof(patch.Color),
                    "Member color is not a valid 6-digit hexadecimal color (eg. 'ff0000')");
        }
    }
}