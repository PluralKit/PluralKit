using Newtonsoft.Json.Linq;

namespace PluralKit.Core;

public class SystemGuildSettings
{
    public ulong Guild { get; }
    public SystemId System { get; }
    public bool ProxyEnabled { get; } = true;
    public string? Tag { get; }
    public bool TagEnabled { get; }
    public string? AvatarUrl { get; }
    public string? DisplayName { get; }
}

public static class SystemGuildExt
{
    public static JObject ToJson(this SystemGuildSettings settings)
    {
        var o = new JObject();

        o.Add("proxying_enabled", settings.ProxyEnabled);
        o.Add("tag", settings.Tag);
        o.Add("tag_enabled", settings.TagEnabled);
        o.Add("avatar_url", settings.AvatarUrl);
        o.Add("display_name", settings.DisplayName);

        return o;
    }
}