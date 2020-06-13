using NodaTime;

namespace PluralKit.Core
{
    public class SystemFronter
    {
        public int SystemId { get; }
        public int SwitchId { get; }
        public Instant SwitchTimestamp { get; }
        public int MemberId { get; }
        public string MemberHid { get; }
        public string MemberName { get; }
    }
}