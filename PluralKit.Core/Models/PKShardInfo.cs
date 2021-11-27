using NodaTime;

namespace PluralKit.Core;

public class PKShardInfo
{
    public enum ShardStatus
    {
        Down = 0,
        Up = 1
    }

    public int Id { get; }
    public ShardStatus Status { get; }
    public float? Ping { get; }
    public Instant? LastHeartbeat { get; }
    public Instant? LastConnection { get; }
}