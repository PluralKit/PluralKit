namespace PluralKit.Core
{
    public class GuildPatch: PatchObject
    {
        public Partial<ulong?> LogChannel { get; set; }
        public Partial<ulong[]> LogBlacklist { get; set; }
        public Partial<ulong[]> Blacklist { get; set; }
        public Partial<bool> LogCleanupEnabled { get; set; }

        public override UpdateQueryBuilder Apply(UpdateQueryBuilder b) => b
            .With("log_channel", LogChannel)
            .With("log_blacklist", LogBlacklist)
            .With("blacklist", Blacklist)
            .With("log_cleanup_enabled", LogCleanupEnabled);
    }
}