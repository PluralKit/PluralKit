#nullable enable

using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core;

public class MemberGuildPatch: PatchObject
{
    public Partial<string?> DisplayName { get; set; }
    public Partial<string?> AvatarUrl { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("display_name", DisplayName)
        .With("avatar_url", AvatarUrl)
    );

    public new void AssertIsValid()
    {
        if (DisplayName.Value != null)
            AssertValid(DisplayName.Value, "display_name", Limits.MaxMemberNameLength);
        if (AvatarUrl.Value != null)
            AssertValid(AvatarUrl.Value, "avatar_url", Limits.MaxUriLength,
                s => MiscUtils.TryMatchUri(s, out var avatarUri));
    }

#nullable disable
    public static MemberGuildPatch FromJson(JObject o)
    {
        var patch = new MemberGuildPatch();

        if (o.ContainsKey("display_name"))
            patch.DisplayName = o.Value<string>("display_name").NullIfEmpty();

        if (o.ContainsKey("avatar_url"))
            patch.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty();

        return patch;
    }

    public JObject ToJson(ulong guild_id)
    {
        var o = new JObject();

        o.Add("guild_id", guild_id.ToString());

        if (DisplayName.IsPresent)
            o.Add("display_name", DisplayName.Value);

        if (AvatarUrl.IsPresent)
            o.Add("avatar_url", AvatarUrl.Value);

        return o;
    }
}