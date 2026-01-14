#nullable enable

using NodaTime;

namespace PluralKit.Core;

/// <summary>
/// Model for the `message_context` PL/pgSQL function in `functions.sql`
/// </summary>
public class MessageContext
{
    public bool AllowAutoproxy { get; }

    public SystemId? SystemId { get; }
    public string? SystemTag { get; }
    public string? SystemAvatar { get; }

    public int? LatchTimeout { get; }
    public bool CaseSensitiveProxyTags { get; }
    public bool ProxyErrorMessageEnabled { get; }
    public SystemConfig.ProxySwitchAction ProxySwitch { get; }
    public string? NameFormat { get; }

    public bool TagEnabled { get; }
    public bool ProxyEnabled { get; }
    public string? SystemGuildTag { get; }
    public string? SystemGuildAvatar { get; }
    public string? GuildNameFormat { get; }

    public SwitchId? LastSwitch { get; }
    public MemberId[] LastSwitchMembers { get; } = new MemberId[0];
    public Instant? LastSwitchTimestamp { get; }

    public ulong? LogChannel { get; }
    public bool InProxyBlacklist { get; }
    public bool InCommandBlacklist { get; }
    public bool InLogBlacklist { get; }
    public bool LogCleanupEnabled { get; }
    public bool RequireSystemTag { get; }
    public bool DenyBotUsage { get; }
    public bool SuppressNotifications { get; }
}