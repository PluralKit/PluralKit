namespace PluralKit.Core
{
    public class AutoproxySettings
    {
        public SystemId Id { get; private set; }
        public AutoproxyMode Mode { get; private set; }
        public AutoproxyScope Scope { get; private set; }
        public ulong Location { get; private set; }
        public MemberId? Member { get; private set; }
    }

    public enum AutoproxyMode
    {
        Off = 1,
        Front = 2,
        Latch = 3,
        Member = 4
    }

    public enum AutoproxyScope
    {
        Global = 1,
        Guild = 2,
        Channel = 3,
    }

}