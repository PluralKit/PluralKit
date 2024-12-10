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
    public bool Tts { get; }
    public bool? ServerKeepProxy { get; }

    public string? ServerName { get; }
    public string? DisplayName { get; }
    public string Name { get; } = "";

    public string? ServerAvatar { get; }
    public string? WebhookAvatar { get; }
    public string? Avatar { get; }

    public string? Color { get; }

    public bool AllowAutoproxy { get; }

    // If not set, this formatting will be applied to the proxy name
    public static string DefaultFormat = "{name} {tag}";

    public static string FormatTag(string template, string? tag, string name) => StringUtils.SafeFormat(template, new[] {
            ("{tag}", tag ?? ""),
            ("{name}", name)
        }).Trim();

    public string ProxyName(MessageContext ctx)
    {
        var memberName = ServerName ?? DisplayName ?? Name;
        var tag = ctx.SystemGuildTag ?? ctx.SystemTag;
        if (!ctx.TagEnabled) tag = null;

        return FormatTag(ctx.GuildNameFormat ?? ctx.NameFormat ?? DefaultFormat, tag, memberName);
    }

    public string? ProxyAvatar(MessageContext ctx) => ServerAvatar ?? WebhookAvatar ?? Avatar ?? ctx.SystemGuildAvatar ?? ctx.SystemAvatar;
}