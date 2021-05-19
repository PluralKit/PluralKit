#nullable enable
namespace PluralKit.Core
{
    public class SystemGuildPatch: PatchObject
    {
        public Partial<bool> ProxyEnabled { get; set; }
        public Partial<AutoproxyMode> AutoproxyMode { get; set; }
        public Partial<MemberId?> AutoproxyMember { get; set; }
        public Partial<string?> Tag { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("proxy_enabled", ProxyEnabled)
            .With("autoproxy_mode", AutoproxyMode)
            .With("autoproxy_member", AutoproxyMember)
            .With("tag", Tag);
    }
}