namespace PluralKit.Core
{
    public class SystemGuildSettings
    {
        public SystemId Guild { get; }
        public bool ProxyEnabled { get; } = true;

        public AutoproxyMode AutoproxyMode { get; } = AutoproxyMode.Off;
        public MemberId? AutoproxyMember { get; }
    }
}