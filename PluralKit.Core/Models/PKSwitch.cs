using NodaTime;

namespace PluralKit.Core {
    public class PKSwitch
    {
        public SwitchId Id { get; }
        public SystemId System { get; set; }
        public Instant Timestamp { get; }
    }
}