using SqlKata;

namespace PluralKit.Core;

public class GuildPatch: PatchObject
{
    public Partial<ulong?> LogChannel { get; set; }
    public Partial<ulong[]> LogBlacklist { get; set; }
    public Partial<ulong[]> ProxyBlacklist { get; set; }
    public Partial<ulong[]> CommandBlacklist { get; set; }
    public Partial<bool> LogCleanupEnabled { get; set; }
    public Partial<bool> InvalidCommandResponseEnabled { get; set; }
    public Partial<bool> RequireSystemTag { get; set; }
    public Partial<bool> SuppressNotifications { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("log_channel", LogChannel)
        .With("log_blacklist", LogBlacklist)
        .With("proxy_blacklist", ProxyBlacklist)
        .With("command_blacklist", CommandBlacklist)
        .With("log_cleanup_enabled", LogCleanupEnabled)
        .With("invalid_command_response_enabled", InvalidCommandResponseEnabled)
        .With("require_system_tag", RequireSystemTag)
        .With("suppress_notifications", SuppressNotifications)
    );
}