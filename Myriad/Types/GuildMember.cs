namespace Myriad.Types
{
    public record GuildMember: GuildMemberPartial
    {
        public User User { get; init; }
    }

    public record GuildMemberPartial
    {
        public string? Avatar { get; init; }
        public string? Nick { get; init; }
        public ulong[] Roles { get; init; }
        public string JoinedAt { get; init; }
    }
}