#nullable enable
using System.Collections.Generic;

namespace PluralKit.Core
{
    /// <summary>
    /// Model for the `proxy_info` PL/pgSQL function in `functions.sql`
    /// </summary>
    public class ProxyMember
    {
        public int SystemId { get; set; }
        public int MemberId { get; set; }
        public bool ProxyEnabled { get; set; }
        public AutoproxyMode AutoproxyMode { get; set; }
        public bool IsAutoproxyMember { get; set; }
        public ulong? LatchMessage { get; set; }
        public string ProxyName { get; set; } = "";
        public string? ProxyAvatar { get; set; }
        public IReadOnlyCollection<ProxyTag> ProxyTags { get; set; } = new ProxyTag[0];
        public bool KeepProxy { get; set; }
        
        public IReadOnlyCollection<ulong> ChannelBlacklist { get; set; } = new ulong[0];
        public IReadOnlyCollection<ulong> LogBlacklist { get; set; } = new ulong[0];
        public ulong? LogChannel { get; set; }
    }
}