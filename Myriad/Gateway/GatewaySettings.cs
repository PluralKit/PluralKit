namespace Myriad.Gateway
{
    public record GatewaySettings
    {
        public string Token { get; init; }
        public GatewayIntent Intents { get; init; }
        public int? MaxShardConcurrency { get; init; }
        public string? GatewayQueueUrl { get; init; }
    }
}