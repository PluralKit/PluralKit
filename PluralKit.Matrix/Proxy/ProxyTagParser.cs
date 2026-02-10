#nullable enable
using PluralKit.Core;

namespace PluralKit.Matrix;

public class ProxyTagParser
{
    public bool TryMatch(IEnumerable<ProxyMember> members, string? input, bool caseSensitive, out ProxyMatch result)
    {
        result = default;

        // Null input is valid and is equivalent to empty string
        if (input == null) return false;

        // "Flatten" list of members to a list of tag-member pairs
        // Then order them by "tag specificity"
        // (prefix+suffix length desc = inner message asc = more specific proxy first)
        var tags = members
            .SelectMany(member => member.ProxyTags.Select(tag => (tag, member)))
            .OrderByDescending(p => p.tag.ProxyString.Length);

        // Iterate now-ordered list of tags and try matching each one
        foreach (var (tag, member) in tags)
        {
            result.ProxyTags = tag;
            result.Member = member;

            // Skip blank tags (shouldn't ever happen in practice)
            if (tag.Prefix == null && tag.Suffix == null) continue;

            // Can we match with these tags?
            if (TryMatchTagsInner(input, tag, caseSensitive, out result.Content))
                return true;
        }

        // We couldn't match anything
        return false;
    }

    private bool TryMatchTagsInner(string input, ProxyTag tag, bool caseSensitive, out string inner)
    {
        inner = "";

        // Normalize null tags to empty strings
        var prefix = tag.Prefix ?? "";
        var suffix = tag.Suffix ?? "";

        var comparison = caseSensitive
            ? StringComparison.CurrentCulture
            : StringComparison.CurrentCultureIgnoreCase;

        // Check if our input starts/ends with the tags
        var isMatch = input.Length >= prefix.Length + suffix.Length
                      && input.StartsWith(prefix, comparison)
                      && input.EndsWith(suffix, comparison);

        // Special case: image-only proxies + proxy tags with spaces
        // Trim everything, then see if we have a "contentless tag pair"
        if (!isMatch && input.Trim() == prefix.TrimEnd() + suffix.TrimStart())
            return true;
        if (!isMatch) return false;

        // We got a match, extract inner text
        inner = input.Substring(prefix.Length, input.Length - prefix.Length - suffix.Length);

        // (see https://github.com/PluralKit/PluralKit/pull/181)
        return inner.Trim() != "\U0000fe0f";
    }
}
