namespace Myriad.Types
{
    public record Channel
    {
        public enum ChannelType
        {
            GuildText = 0,
            Dm = 1,
            GuildVoice = 2,
            GroupDm = 3,
            GuildCategory = 4,
            GuildNews = 5,
            GuildStore = 6,
            GuildNewsThread = 10,
            GuildPublicThread = 11,
            GuildPrivateThread = 12,
            GuildStageVoice = 13
        }

        public ulong Id { get; init; }
        public ChannelType Type { get; init; }
        public ulong? GuildId { get; init; }
        public int? Position { get; init; }
        public string? Name { get; init; }
        public string? Topic { get; init; }
        public bool? Nsfw { get; init; }
        public ulong? ParentId { get; init; }
        public Overwrite[]? PermissionOverwrites { get; init; }
        public User[]? Recipients { get; init; } // NOTE: this may be null for stub channel objects

        public record Overwrite
        {
            public ulong Id { get; init; }
            public OverwriteType Type { get; init; }
            public PermissionSet Allow { get; init; }
            public PermissionSet Deny { get; init; }
        }

        public enum OverwriteType
        {
            Role = 0,
            Member = 1
        }
    }
}