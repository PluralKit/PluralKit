#nullable enable
namespace PluralKit.Core
{
    public class MemberGuildSettings
    {
        public int Member { get; }
        public ulong Guild { get; }
        public string? DisplayName { get; }
        public string? AvatarUrl { get; }
    }
}