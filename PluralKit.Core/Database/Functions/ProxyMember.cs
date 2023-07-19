#nullable enable
namespace PluralKit.Core;

/// <summary>
///  Model for the `proxy_members` PL/pgSQL function in `functions.sql`
/// </summary>
public class ProxyMember
{
    public ProxyMember() { }

    public ProxyMember(string name, params ProxyTag[] tags)
    {
        Name = name;
        ProxyTags = tags;
    }

    public MemberId Id { get; }
    public IReadOnlyCollection<ProxyTag> ProxyTags { get; } = new ProxyTag[0];
    public bool KeepProxy { get; }

    public string? ServerName { get; }
    public string? DisplayName { get; }
    public string Name { get; } = "";

    public string? ServerAvatar { get; }
    public string? WebhookAvatar { get; }
    public string? Avatar { get; }

    public bool AllowAutoproxy { get; }
    public string? Color { get; }

    public string ProxyName(MessageContext ctx)
    {
        var memberName = ServerName ?? DisplayName ?? Name;
        if (!ctx.TagEnabled)
            return memberName;

        if (ctx.SystemGuildTag != null)
            return $"{memberName} {ctx.SystemGuildTag}";
        if (ctx.SystemTag != null)
            return $"{memberName} {ctx.SystemTag}";
        return memberName;
    }

    public string? ProxyAvatar(MessageContext ctx) => ServerAvatar ?? WebhookAvatar ?? Avatar ?? ctx.SystemGuildAvatar ?? ctx.SystemAvatar;
}