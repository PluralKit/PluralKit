using NodaTime;

namespace PluralKit.Core {
    public class PKSwitch
    {
        public int Id { get; }
        public int System { get; set; }
        public Instant Timestamp { get; }
    }
}