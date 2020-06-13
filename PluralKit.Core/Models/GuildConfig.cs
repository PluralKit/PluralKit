namespace PluralKit.Core
{
    public class GuildConfig
    {
        public int Id { get; }
        public ulong? LogChannel { get; }
        public ulong[] LogBlacklist { get; }
        public ulong[] Blacklist { get; }
        public bool LogCleanupEnabled { get; }
    }
}