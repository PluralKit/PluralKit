#nullable enable

namespace PluralKit.Core;
public static class MessageContextExt
{
    public static bool HasProxyableTag(this MessageContext ctx)
    {
        var tag = ctx.SystemGuildTag ?? ctx.SystemTag;
        if (!ctx.TagEnabled || tag == null)
            return false;

        var format = ctx.GuildNameFormat ?? ctx.NameFormat ?? ProxyMember.DefaultFormat;
        if (!format.Contains("{tag}"))
            return false;

        return true;
    }
}