#nullable enable
namespace PluralKit.Core
{
    public class MemberGuildSettings
    {
        public MemberId Member { get; }
        public ulong Guild { get; }
        public string? DisplayName { get; }
        public string? AvatarUrl { get; }
    }
}