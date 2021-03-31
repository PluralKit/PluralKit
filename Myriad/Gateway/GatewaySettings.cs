namespace Myriad.Gateway
{
    public record GatewaySettings
    {
        public string Token { get; init; }
        public GatewayIntent Intents { get; init; }
    }
}