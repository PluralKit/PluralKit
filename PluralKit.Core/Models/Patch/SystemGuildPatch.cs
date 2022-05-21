#nullable enable

using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core;

public class SystemGuildPatch: PatchObject
{
    public Partial<bool> ProxyEnabled { get; set; }
    public Partial<string?> Tag { get; set; }
    public Partial<bool?> TagEnabled { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("proxy_enabled", ProxyEnabled)
        .With("tag", Tag)
        .With("tag_enabled", TagEnabled)
    );

    public new void AssertIsValid()
    {
        if (Tag.Value != null)
            AssertValid(Tag.Value, "tag", Limits.MaxSystemTagLength);
    }

#nullable disable
    public static SystemGuildPatch FromJson(JObject o)
    {
        var patch = new SystemGuildPatch();

        if (o.ContainsKey("proxying_enabled") && o["proxying_enabled"].Type != JTokenType.Null)
            patch.ProxyEnabled = o.Value<bool>("proxying_enabled");

        if (o.ContainsKey("tag"))
            patch.Tag = o.Value<string>("tag").NullIfEmpty();

        if (o.ContainsKey("tag_enabled") && o["tag_enabled"].Type != JTokenType.Null)
            patch.TagEnabled = o.Value<bool>("tag_enabled");

        return patch;
    }

    public JObject ToJson(ulong guild_id)
    {
        var o = new JObject();

        o.Add("guild_id", guild_id.ToString());

        if (ProxyEnabled.IsPresent)
            o.Add("proxying_enabled", ProxyEnabled.Value);

        if (Tag.IsPresent)
            o.Add("tag", Tag.Value);

        if (TagEnabled.IsPresent)
            o.Add("tag_enabled", TagEnabled.Value);

        return o;
    }
}