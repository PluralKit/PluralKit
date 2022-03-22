using Newtonsoft.Json.Linq;

namespace PluralKit.Core;

public class SystemGuildSettings
{
    public ulong Guild { get; }
    public SystemId System { get; }
    public bool ProxyEnabled { get; } = true;
    public string? Tag { get; }
    public bool TagEnabled { get; }
}

public static class SystemGuildExt
{
    public static JObject ToJson(this SystemGuildSettings settings)
    {
        var o = new JObject();

        o.Add("proxying_enabled", settings.ProxyEnabled);
        o.Add("tag", settings.Tag);
        o.Add("tag_enabled", settings.TagEnabled);

        return o;
    }
}