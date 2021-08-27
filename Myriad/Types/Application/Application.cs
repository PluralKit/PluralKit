namespace Myriad.Types
{
    public record Application: ApplicationPartial
    {
        public string Name { get; init; }
        public string? Icon { get; init; }
        public string Description { get; init; }
        public string[]? RpcOrigins { get; init; }
        public bool BotPublic { get; init; }
        public bool BotRequireCodeGrant { get; init; }
        public User Owner { get; init; } // TODO: docs specify this is "partial", what does that mean
        public string Summary { get; init; }
        public string VerifyKey { get; init; }
        public ulong? GuildId { get; init; }
        public ulong? PrimarySkuId { get; init; }
        public string? Slug { get; init; }
        public string? CoverImage { get; init; }
    }

    public record ApplicationPartial
    {
        public ulong Id { get; init; }
        public int Flags { get; init; }
    }
}