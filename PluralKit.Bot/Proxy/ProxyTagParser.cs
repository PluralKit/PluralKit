#nullable enable
using System.Collections.Generic;
using System.Linq;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class ProxyTagParser
    {
        public bool TryParse(string input, IEnumerable<PKMember> members, out ProxyMatch result)
        {
            result = default;
            
            // If the message starts with a @mention, and then proceeds to have proxy tags,
            // extract the mention and place it inside the inner message
            // eg. @Ske [text] => [@Ske text]
            var leadingMention = ExtractLeadingMention(ref input);

            // "Flatten" list of members to a list of tag-member pairs
            // Then order them by "tag specificity"
            // (ProxyString length desc = prefix+suffix length desc = inner message asc = more specific proxy first)
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
                if (TryMatchTags(input, tag, out result.Content))
                {
                    // (see https://github.com/xSke/PluralKit/pull/181)
                    if (result.Content == "\U0000fe0f") return false;
                    
                    // If we extracted a leading mention before, add that back now
                    if (leadingMention != null) result.Content = $"{leadingMention} {result.Content}";
                    
                    // We're done!
                    return true;
                }
                
                // (if not, keep going)
            }
            
            // We couldn't match anything :(
            return false;
        }

        private bool TryMatchTags(string input, ProxyTag tag, out string content)
        {
            // Normalize null tags to empty strings
            var prefix = tag.Prefix ?? "";
            var suffix = tag.Suffix ?? "";
                
            // Check if our input starts/ends with the tags
            var isMatch = input.Length >= prefix.Length + suffix.Length 
                          && input.StartsWith(prefix) && input.EndsWith(suffix);
            
            // Special case: image-only proxies + proxy tags with spaces
            // Trim everything, then see if we have a "contentless tag pair" (normally disallowed, but OK if we have an attachment)
            if (!isMatch && input.Trim() == prefix.TrimEnd() + suffix.TrimStart())
            {
                content = ""; 
                return true;
            }

            if (isMatch)
            {
                content = input.Substring(prefix.Length, input.Length - prefix.Length - suffix.Length);
                return true;
            }

            content = "";
            return false;
        }

        private string? ExtractLeadingMention(ref string input)
        {
            var mentionPos = 0;
            if (!StringUtils.HasMentionPrefix(input, ref mentionPos, out _)) return null;
            
            var leadingMention = input.Substring(0, mentionPos);
            input = input.Substring(mentionPos);
            return leadingMention;
        }
    }
}