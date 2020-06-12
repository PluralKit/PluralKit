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
        public int? SystemId { get; set; }
        public ulong? LogChannel { get; set; }
        public bool InBlacklist { get; set; }
        public bool InLogBlacklist { get; set; }
        public bool LogCleanupEnabled { get; set; }
        public bool ProxyEnabled { get; set; }
        public AutoproxyMode AutoproxyMode { get; set; }
        public int? AutoproxyMember { get; set; }
        public ulong? LastMessage { get; set; }
        public int? LastMessageMember { get; set; }
        public int LastSwitch { get; set; }
        public IReadOnlyList<int> LastSwitchMembers { get; set; } = new int[0];
        public Instant LastSwitchTimestamp { get; set; }
    }
}