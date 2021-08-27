namespace Myriad.Types
{
    public record Guild
    {
        public ulong Id { get; init; }
        public string Name { get; init; }
        public string? Icon { get; init; }
        public string? Splash { get; init; }
        public string? DiscoverySplash { get; init; }
        public bool? Owner { get; init; }
        public ulong OwnerId { get; init; }
        public string Region { get; init; }
        public ulong? AfkChannelId { get; init; }
        public int AfkTimeout { get; init; }
        public bool? WidgetEnabled { get; init; }
        public ulong? WidgetChannelId { get; init; }
        public int VerificationLevel { get; init; }

        public Role[] Roles { get; init; }
        public string[] Features { get; init; }
    }
}