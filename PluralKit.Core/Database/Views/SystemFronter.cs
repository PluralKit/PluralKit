using NodaTime;

namespace PluralKit.Core
{
    public class SystemFronter
    {
        public SystemId SystemId { get; }
        public SwitchId SwitchId { get; }
        public Instant SwitchTimestamp { get; }
        public MemberId MemberId { get; }
        public string MemberHid { get; }
        public string MemberName { get; }
    }
}