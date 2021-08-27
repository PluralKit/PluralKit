namespace Myriad.Types
{
    public record GatewayInfo
    {
        public string Url { get; init; }

        public record Bot: GatewayInfo
        {
            public int Shards { get; init; }
            public SessionStartLimit SessionStartLimit { get; init; }
        }
    }
}