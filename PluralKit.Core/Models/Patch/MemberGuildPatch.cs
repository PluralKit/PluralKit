#nullable enable

using SqlKata;

namespace PluralKit.Core
{
    public class MemberGuildPatch: PatchObject
    {
        public Partial<string?> DisplayName { get; set; }
        public Partial<string?> AvatarUrl { get; set; }

        public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
            .With("display_name", DisplayName)
            .With("avatar_url", AvatarUrl)
        );
    }
}