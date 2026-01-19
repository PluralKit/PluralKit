namespace PluralKit.Core;

public class GuildConfig
{
    public ulong Id { get; }
    public ulong? LogChannel { get; }
    public ulong[] LogBlacklist { get; }
    public ulong[] ProxyBlacklist { get; }
    public ulong[] CommandBlacklist { get; }
    public bool LogCleanupEnabled { get; }
    public bool InvalidCommandResponseEnabled { get; }
    public bool RequireSystemTag { get; }
    public bool SuppressNotifications { get; }
}