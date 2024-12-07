using NodaTime;

namespace PluralKit.Core;

public readonly struct AbuseLogId: INumericId<AbuseLogId, int>
{
    public int Value { get; }

    public AbuseLogId(int value)
    {
        Value = value;
    }

    public bool Equals(AbuseLogId other) => Value == other.Value;

    public override bool Equals(object obj) => obj is AbuseLogId other && Equals(other);

    public override int GetHashCode() => Value;

    public static bool operator ==(AbuseLogId left, AbuseLogId right) => left.Equals(right);

    public static bool operator !=(AbuseLogId left, AbuseLogId right) => !left.Equals(right);

    public int CompareTo(AbuseLogId other) => Value.CompareTo(other.Value);

    public override string ToString() => $"AbuseLog #{Value}";
}

public class AbuseLog
{
    public AbuseLogId Id { get; private set; }
    public Guid Uuid { get; private set; }
    public string Description { get; private set; }
    public bool DenyBotUsage { get; private set; }
    public Instant Created { get; private set; }
}