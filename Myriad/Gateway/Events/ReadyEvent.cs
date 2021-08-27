using System.Text.Json.Serialization;

using Myriad.Types;

namespace Myriad.Gateway
{
    public record ReadyEvent: IGatewayEvent
    {
        [JsonPropertyName("v")] public int Version { get; init; }
        public User User { get; init; }
        public string SessionId { get; init; }
        public ShardInfo? Shard { get; init; }
        public ApplicationPartial Application { get; init; }
    }
}