using Newtonsoft.Json.Linq;

namespace PluralKit.Matrix;

public class MatrixEvent
{
    public string Type { get; private set; } = "";
    public string EventId { get; private set; } = "";
    public string RoomId { get; private set; } = "";
    public string Sender { get; private set; } = "";
    public long OriginServerTs { get; private set; }
    public JObject Content { get; private set; } = new();
    public JObject? Unsigned { get; private set; }
    public string? TopLevelRedacts { get; private set; }

    // Message helpers
    public string? MessageType => Content["msgtype"]?.Value<string>();
    public string? Body => Content["body"]?.Value<string>();
    public string? FormattedBody => Content["formatted_body"]?.Value<string>();

    // Edit detection (m.replace relation)
    public bool IsEdit => Content["m.relates_to"]?["rel_type"]?.Value<string>() == "m.replace";
    public string? EditedEventId => Content["m.relates_to"]?["event_id"]?.Value<string>();
    public JObject? NewContent => Content["m.new_content"] as JObject;

    // Redaction (top-level for room versions <11, content.redacts for v11+)
    public string? RedactedEventId => TopLevelRedacts ?? Content["redacts"]?.Value<string>();

    // Reaction
    public string? ReactionKey => Content["m.relates_to"]?["key"]?.Value<string>();
    public string? ReactionTargetEventId => Content["m.relates_to"]?["event_id"]?.Value<string>();

    public bool IsValid => !string.IsNullOrEmpty(Type) && !string.IsNullOrEmpty(EventId)
        && !string.IsNullOrEmpty(RoomId) && !string.IsNullOrEmpty(Sender);

    public static MatrixEvent FromJson(JObject json)
    {
        return new MatrixEvent
        {
            Type = json["type"]?.Value<string>() ?? "",
            EventId = json["event_id"]?.Value<string>() ?? "",
            RoomId = json["room_id"]?.Value<string>() ?? "",
            Sender = json["sender"]?.Value<string>() ?? "",
            OriginServerTs = json["origin_server_ts"]?.Value<long>() ?? 0,
            Content = json["content"] as JObject ?? new JObject(),
            Unsigned = json["unsigned"] as JObject,
            TopLevelRedacts = json["redacts"]?.Value<string>(),
        };
    }
}
