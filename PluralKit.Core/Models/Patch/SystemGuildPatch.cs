#nullable enable

using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core;

public class SystemGuildPatch: PatchObject
{
    public Partial<bool> ProxyEnabled { get; set; }
    public Partial<string?> Tag { get; set; }
    public Partial<bool?> TagEnabled { get; set; }
    public Partial<string?> AvatarUrl { get; set; }
    public Partial<string?> DisplayName { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("proxy_enabled", ProxyEnabled)
        .With("tag", Tag)
        .With("tag_enabled", TagEnabled)
        .With("avatar_url", AvatarUrl)
        .With("display_name", DisplayName)
    );

    public new void AssertIsValid()
    {
        if (Tag.Value != null)
            AssertValid(Tag.Value, "tag", Limits.MaxSystemTagLength);
        if (AvatarUrl.Value != null)
            AssertValid(AvatarUrl.Value, "avatar_url", Limits.MaxUriLength,
                s => MiscUtils.TryMatchUri(s, out var avatarUri));
        if (DisplayName.Value != null)
            AssertValid(DisplayName.Value, "display_name", Limits.MaxMemberNameLength);
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

        if (o.ContainsKey("avatar_url"))
            patch.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty();

        if (o.ContainsKey("display_name"))
            patch.DisplayName = o.Value<string>("display_name").NullIfEmpty();

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

        if (AvatarUrl.IsPresent)
            o.Add("avatar_url", AvatarUrl.Value);

        if (DisplayName.IsPresent)
            o.Add("display_name", DisplayName.Value);

        return o;
    }
}