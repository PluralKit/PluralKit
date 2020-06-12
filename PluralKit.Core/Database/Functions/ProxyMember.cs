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
        
        public string? ServerName { get; set; }
        public string? DisplayName { get; set; }
        public string Name { get; set; } = "";
        
        public string? ServerAvatar { get; set; }
        public string? Avatar { get; set; }

        public string ProxyName(MessageContext ctx) => ctx.SystemTag != null
            ? $"{ServerName ?? DisplayName ?? Name} {ctx.SystemTag}"
            : ServerName ?? DisplayName ?? Name;

        public string? ProxyAvatar(MessageContext ctx) => ServerAvatar ?? Avatar ?? ctx.SystemAvatar;
    }
}