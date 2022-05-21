using Newtonsoft.Json.Linq;

#nullable enable
namespace PluralKit.Core;

public class MemberGuildSettings
{
    public MemberId Member { get; }
    public ulong Guild { get; }
    public string? DisplayName { get; }
    public string? AvatarUrl { get; }
}

public static class MemberGuildExt
{
    public static JObject ToJson(this MemberGuildSettings settings)
    {
        var o = new JObject();

        o.Add("display_name", settings.DisplayName);
        o.Add("avatar_url", settings.AvatarUrl);

        return o;
    }
}