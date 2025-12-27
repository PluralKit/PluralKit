using Newtonsoft.Json.Linq;
using NodaTime;

namespace PluralKit.Core;

public class HidChangelog
{
    public int Id { get; }
    public SystemId System { get; }
    public ulong DiscordUid { get; }
    public string HidType { get; }
    public string HidOld { get; }
    public string HidNew { get; }
    public Instant Created { get; }
}