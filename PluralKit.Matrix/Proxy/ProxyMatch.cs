#nullable enable
using PluralKit.Core;

namespace PluralKit.Matrix;

public struct ProxyMatch
{
    public ProxyMember Member;
    public string? Content;
    public ProxyTag? ProxyTags;

    public string? ProxyContent
    {
        get
        {
            // In Matrix, follow the member's keep_proxy setting
            // No server-specific override (no guild system in Matrix)
            if (Member.KeepProxy && ProxyTags != null && Content != null)
                return $"{ProxyTags.Value.Prefix}{Content}{ProxyTags.Value.Suffix}";

            return Content;
        }
    }
}
