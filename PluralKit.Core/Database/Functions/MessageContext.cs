#nullable enable
using System.Collections.Generic;

using NodaTime;

namespace PluralKit.Core
{
    /// <summary>
    /// Model for the `message_context` PL/pgSQL function in `functions.sql`
    /// </summary>
    public class MessageContext
    {
        public int? SystemId { get; }
        public ulong? LogChannel { get; }
        public bool InBlacklist { get; }
        public bool InLogBlacklist { get; }
        public bool LogCleanupEnabled { get; }
        public bool ProxyEnabled { get; }
        public AutoproxyMode AutoproxyMode { get; }
        public int? AutoproxyMember { get; }
        public ulong? LastMessage { get; }
        public int? LastMessageMember { get; }
        public int LastSwitch { get; }
        public IReadOnlyList<int> LastSwitchMembers { get; } = new int[0];
        public Instant LastSwitchTimestamp { get; }
        public string? SystemTag { get; }
        public string? SystemAvatar { get; }
    }
}