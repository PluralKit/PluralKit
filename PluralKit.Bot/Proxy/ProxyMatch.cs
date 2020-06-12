#nullable enable
using PluralKit.Core;

namespace PluralKit.Bot
{
    public struct ProxyMatch
    {
        public ProxyMember Member;
        public string? Content;
        public ProxyTag? ProxyTags;
        
        public string? ProxyContent
        {
            get
            {
                // Add the proxy tags into the proxied message if that option is enabled
                // Also check if the member has any proxy tags - some cases autoproxy can return a member with no tags
                if (Member.KeepProxy && Content != null && ProxyTags != null)
                    return $"{ProxyTags.Value.Prefix}{Content}{ProxyTags.Value.Suffix}";

                return Content;
            }
        }
    }
}