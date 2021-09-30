#nullable enable

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
    }
}