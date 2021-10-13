#nullable enable

using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core
{
    public class SystemGuildPatch: PatchObject
    {
        public Partial<bool> ProxyEnabled { get; set; }
        public Partial<AutoproxyMode> AutoproxyMode { get; set; }
        public Partial<MemberId?> AutoproxyMember { get; set; }
        public Partial<string?> Tag { get; set; }
        public Partial<bool?> TagEnabled { get; set; }

        public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
            .With("proxy_enabled", ProxyEnabled)
            .With("autoproxy_mode", AutoproxyMode)
            .With("autoproxy_member", AutoproxyMember)
            .With("tag", Tag)
            .With("tag_enabled", TagEnabled)
        );

        public new void AssertIsValid()
        {
            if (Tag.Value != null)
                AssertValid(Tag.Value, "tag", Limits.MaxSystemTagLength);
        }

#nullable disable
        public static SystemGuildPatch FromJson(JObject o, MemberId? memberId)
        {
            var patch = new SystemGuildPatch();

            if (o.ContainsKey("proxying_enabled") && o["proxying_enabled"].Type != JTokenType.Null)
                patch.ProxyEnabled = o.Value<bool>("proxying_enabled");

            if (o.ContainsKey("autoproxy_mode") && o["autoproxy_mode"].ParseAutoproxyMode() is { } autoproxyMode)
                patch.AutoproxyMode = autoproxyMode;

            patch.AutoproxyMember = memberId;

            if (o.ContainsKey("tag"))
                patch.Tag = o.Value<string>("tag").NullIfEmpty();

            if (o.ContainsKey("tag_enabled") && o["tag_enabled"].Type != JTokenType.Null)
                patch.TagEnabled = o.Value<bool>("tag_enabled");

            return patch;
        }
    }
}