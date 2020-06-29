#nullable enable
namespace PluralKit.Core
{
    public class MemberGuildPatch: PatchObject
    {
        public Partial<string?> DisplayName { get; set; }
        public Partial<string?> AvatarUrl { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("display_name", DisplayName)
            .With("avatar_url", AvatarUrl);
    }
}