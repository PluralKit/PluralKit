namespace PluralKit.Core
{
    public enum AutoproxyMode
    {
        Off = 1,
        Front = 2,
        Latch = 3,
        Member = 4
    }
    
    public class SystemGuildSettings
    {
        public ulong Guild { get; }
        public SystemId System { get; }
        public bool ProxyEnabled { get; } = true;

        public AutoproxyMode AutoproxyMode { get; } = AutoproxyMode.Off;
        public MemberId? AutoproxyMember { get; }
    }
}