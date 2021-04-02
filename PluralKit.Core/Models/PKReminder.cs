using NodaTime;
using PluralKit.Core;

public class PKReminder {
    public ulong Mid { get; set; }
    public ulong Channel { get; set; }
    public ulong? Guild { get; set; }
    public MemberId? Member { get; set; }
    public SystemId System { get; set; }
    public bool Seen { get; set; }
    public Instant Timestamp { get; set; }
}