namespace Myriad.Gateway;

public record GatewaySettings
{
    public string Token { get; init; }
    public GatewayIntent Intents { get; init; }
    public bool UseRedisRatelimiter { get; init; } = false;
    public int? MaxShardConcurrency { get; init; }
    public string? GatewayQueueUrl { get; init; }
}