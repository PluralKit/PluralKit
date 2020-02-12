using NodaTime;

namespace PluralKit.Core {
    public class PKSwitch
    {
        public int Id { get; set; }
        public int System { get; set; }
        public Instant Timestamp { get; set; }
    }
    
    public class PKSwitchMember
    {
        public int Id { get; set; }
        public int Switch { get; set; }
        public int Member { get; set; }
    }
}