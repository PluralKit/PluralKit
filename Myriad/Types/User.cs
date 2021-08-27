using System;

namespace Myriad.Types
{
    public record User
    {
        [Flags]
        public enum Flags
        {
            DiscordEmployee = 1 << 0,
            PartneredServerOwner = 1 << 1,
            HypeSquadEvents = 1 << 2,
            BugHunterLevel1 = 1 << 3,
            HouseBravery = 1 << 6,
            HouseBrilliance = 1 << 7,
            HouseBalance = 1 << 8,
            EarlySupporter = 1 << 9,
            TeamUser = 1 << 10,
            System = 1 << 12,
            BugHunterLevel2 = 1 << 14,
            VerifiedBot = 1 << 16,
            EarlyVerifiedBotDeveloper = 1 << 17
        }

        public ulong Id { get; init; }
        public string Username { get; init; }
        public string Discriminator { get; init; }
        public string? Avatar { get; init; }
        public bool Bot { get; init; }
        public bool? System { get; init; }
        public Flags PublicFlags { get; init; }

        public record Extra: User
        {
            public GuildMemberPartial? Member { get; init; }
        }
    }
}