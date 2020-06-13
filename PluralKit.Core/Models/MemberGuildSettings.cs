#nullable enable
namespace PluralKit.Core
{
    public class MemberGuildSettings
    {
        public int Member { get; }
        public ulong Guild { get; }
        public string? DisplayName { get; }
        public string? AvatarUrl { get; }

        public MemberGuildSettings() { }

        public MemberGuildSettings(int member, ulong guild)
        {
            Member = member;
            Guild = guild;
        }
    }
}