namespace PluralKit.Core
{
    public class SystemGuildSettings
    {
        public ulong Guild { get; }
        public bool ProxyEnabled { get; } = true;

        public AutoproxyMode AutoproxyMode { get; } = AutoproxyMode.Off;
        public int? AutoproxyMember { get; }
    }
}