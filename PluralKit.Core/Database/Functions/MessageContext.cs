#nullable enable

using NodaTime;

namespace PluralKit.Core
{
    /// <summary>
    /// Model for the `message_context` PL/pgSQL function in `functions.sql`
    /// </summary>
    public class MessageContext
    {
        public SystemId? SystemId { get; }
        public ulong? LogChannel { get; }
        public bool InBlacklist { get; }
        public bool InLogBlacklist { get; }
        public bool LogCleanupEnabled { get; }
        public bool ProxyEnabled { get; }
        public AutoproxyMode AutoproxyMode { get; }
        public MemberId? AutoproxyMember { get; }
        public ulong? LastMessage { get; }
        public MemberId? LastMessageMember { get; }
        public SwitchId? LastSwitch { get; }
        public MemberId[] LastSwitchMembers { get; } = new MemberId[0];
        public Instant? LastSwitchTimestamp { get; }
        public string? SystemTagSuffix { get; }
        public string? SystemTagPrefix { get; }

        public string? SystemAvatar { get; }
    }
}