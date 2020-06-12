#nullable enable
using System.Collections.Generic;

namespace PluralKit.Core
{
    /// <summary>
    /// Model for the `proxy_members` PL/pgSQL function in `functions.sql`
    /// </summary>
    public class ProxyMember
    {
        public int Id { get; set; }
        public IReadOnlyCollection<ProxyTag> ProxyTags { get; set; } = new ProxyTag[0];
        public bool KeepProxy { get; set; }
        public string ProxyName { get; set; } = "";
        public string? ProxyAvatar { get; set; }
    }
}