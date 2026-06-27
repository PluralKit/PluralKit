namespace PluralKit.Core;

public class GuildConfig
{
    public ulong Id { get; }
    public ulong? LogChannel { get; }
    public ulong[] LogBlacklist { get; }
    public ulong[] Blacklist { get; }
    public bool LogCleanupEnabled { get; }
    public bool InvalidCommandResponseEnabled { get; }
    public bool RequireSystemTag { get; }
    public SuppressCondition SuppressNotifications { get; }

    public enum SuppressCondition
    {
        Never = 0,
        Always = 1,
        Match = 2,
        Invert = 3,
    }
}

public static class GuildConfigExt
{
    public static string ToUserString(this GuildConfig.SuppressCondition val) => val.ToString().ToLower();
}