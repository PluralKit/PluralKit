namespace Myriad.Types
{
    public record SessionStartLimit
    {
        public int Total { get; init; }
        public int Remaining { get; init; }
        public int ResetAfter { get; init; }
        public int MaxConcurrency { get; init; }
    }
}