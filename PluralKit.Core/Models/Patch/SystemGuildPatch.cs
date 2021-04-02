#nullable enable
namespace PluralKit.Core
{
    public class SystemGuildPatch: PatchObject
    {
        public Partial<bool> ProxyEnabled { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("proxy_enabled", ProxyEnabled);
    }
}