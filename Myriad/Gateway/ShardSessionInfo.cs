namespace Myriad.Gateway
{
    public record ShardSessionInfo
    {
        public string? Session { get; init; }
        public int? LastSequence { get; init; }
    }
}