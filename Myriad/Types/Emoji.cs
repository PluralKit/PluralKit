namespace Myriad.Types
{
    public record Emoji
    {
        public ulong? Id { get; init; }
        public string? Name { get; init; }
        public bool? Animated { get; init; }
    }
}