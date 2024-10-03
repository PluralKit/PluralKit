#nullable enable
using PluralKit.Core;

namespace PluralKit.Bot;

public struct ProxyMatch
{
    public ProxyMember Member;
    public string? Content;
    public ProxyTag? ProxyTags;

    private bool ShouldKeepProxy()
    {
        if (Member.ServerKeepProxy != null && Member.ServerKeepProxy.Value)
            return true;
        else if (Member.KeepProxy && !(Member.ServerKeepProxy != null && !Member.ServerKeepProxy.Value))
            return true;
        else return false;
    }

    public string? ProxyContent
    {
        get
        {
            // Add the proxy tags into the proxied message if that option is enabled
            // Also check if the member has any proxy tags - some cases autoproxy can return a member with no tags
            if (ShouldKeepProxy() && ProxyTags != null && Content != null)
                return $"{ProxyTags.Value.Prefix}{Content}{ProxyTags.Value.Suffix}";

            return Content;
        }
    }
}