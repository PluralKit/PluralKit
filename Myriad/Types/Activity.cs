namespace Myriad.Types
{
    public record Activity: ActivityPartial
    {
    }

    public record ActivityPartial
    {
        public string Name { get; init; }
        public ActivityType Type { get; init; }
        public string? Url { get; init; }
    }

    public enum ActivityType
    {
        Game = 0,
        Streaming = 1,
        Listening = 2,
        Custom = 4,
        Competing = 5
    }
}