using System.Text.Json.Serialization;

namespace Myriad.Gateway
{
    public record GatewayPacket
    {
        [JsonPropertyName("op")] public GatewayOpcode Opcode { get; init; }
        [JsonPropertyName("d")] public object? Payload { get; init; }

        [JsonPropertyName("s")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Sequence { get; init; }

        [JsonPropertyName("t")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EventType { get; init; }
    }

    public enum GatewayOpcode
    {
        Dispatch = 0,
        Heartbeat = 1,
        Identify = 2,
        PresenceUpdate = 3,
        VoiceStateUpdate = 4,
        Resume = 6,
        Reconnect = 7,
        RequestGuildMembers = 8,
        InvalidSession = 9,
        Hello = 10,
        HeartbeatAck = 11
    }
}