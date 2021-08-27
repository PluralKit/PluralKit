using System.Text.Json.Serialization;

namespace Myriad.Gateway
{
    public record GatewayIdentify
    {
        public string Token { get; init; }
        public ConnectionProperties Properties { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Compress { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? LargeThreshold { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ShardInfo? Shard { get; init; }

        public GatewayIntent Intents { get; init; }

        public record ConnectionProperties
        {
            [JsonPropertyName("$os")] public string Os { get; init; }
            [JsonPropertyName("$browser")] public string Browser { get; init; }
            [JsonPropertyName("$device")] public string Device { get; init; }
        }
    }
}