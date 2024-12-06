#nullable enable

using NodaTime;

namespace PluralKit.Core;

/// <summary>
/// Model for the `message_context` PL/pgSQL function in `functions.sql`
/// </summary>
public class MessageContext
{
    public SystemId? SystemId { get; }

    /// <summary>
    /// Whether a system is being deleted (no actions should be taken, or commands ran)
    /// </summary>
    public ulong? LogChannel { get; }
    public bool InBlacklist { get; }
    public bool InLogBlacklist { get; }
    public bool LogCleanupEnabled { get; }
    public bool RequireSystemTag { get; }
    public bool ProxyEnabled { get; }
    public SwitchId? LastSwitch { get; }
    public MemberId[] LastSwitchMembers { get; } = new MemberId[0];
    public Instant? LastSwitchTimestamp { get; }
    public string? SystemTag { get; }
    public string? SystemGuildTag { get; }
    public bool TagEnabled { get; }
    public string? NameFormat { get; }
    public string? GuildNameFormat { get; }
    public string? SystemAvatar { get; }
    public string? SystemGuildAvatar { get; }
    public bool AllowAutoproxy { get; }
    public int? LatchTimeout { get; }
    public bool CaseSensitiveProxyTags { get; }
    public bool ProxyErrorMessageEnabled { get; }
    public SystemConfig.ProxySwitchAction ProxySwitch { get; }
    public bool DenyBotUsage { get; }
}