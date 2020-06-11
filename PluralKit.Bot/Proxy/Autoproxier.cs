#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;

using NodaTime;

using PluralKit.Core;


namespace PluralKit.Bot
{
    public class Autoproxier
    {
        public static readonly string EscapeString = @"\"; 
        public static readonly Duration AutoproxyExpiryTime = Duration.FromHours(6);

        private IClock _clock;
        private IDataStore _data;
        
        public Autoproxier(IDataStore data, IClock clock)
        {
            _data = data;
            _clock = clock;
        }

        public async ValueTask<ProxyMatch?> TryAutoproxy(AutoproxyContext ctx)
        {
            if (IsEscaped(ctx.Content))
                return null;

            var member = await FindAutoproxyMember(ctx);
            if (member == null) return null;
            
            return new ProxyMatch
            {
                Content = ctx.Content,
                Member = member,
                ProxyTags = ProxyTagsFor(member)
            };
        }
        
        private async ValueTask<PKMember?> FindAutoproxyMember(AutoproxyContext ctx)
        {
            switch (ctx.Mode)
            {
                case AutoproxyMode.Off:
                    return null;
                
                case AutoproxyMode.Front:
                    return await _data.GetFirstFronter(ctx.Account.System);
                
                case AutoproxyMode.Latch:
                    // Latch mode: find last proxied message, use *that* member
                    var msg = await _data.GetLastMessageInGuild(ctx.SenderId, ctx.GuildId);
                    if (msg == null) return null; // No message found

                    // If the message is older than 6 hours, ignore it and force the sender to "refresh" a proxy
                    // This can be revised in the future, it's a preliminary value.
                    var timestamp = DiscordUtils.SnowflakeToInstant(msg.Message.Mid);
                    if (_clock.GetCurrentInstant() - timestamp > AutoproxyExpiryTime) return null;
                    
                    return msg.Member;
                
                case AutoproxyMode.Member:
                    // We already have the member list cached, so:
                    // O(n) lookup since n is small (max 1500 de jure) and we're more constrained by memory (for a dictionary) here
                    return ctx.Account.Members.FirstOrDefault(m => m.Id == ctx.AutoproxyMember);
                
                default: 
                    throw new ArgumentOutOfRangeException($"Unknown autoproxy mode {ctx.Mode}");
            }
        }

        private ProxyTag? ProxyTagsFor(PKMember member)
        {
            if (member.ProxyTags.Count == 0) return null;
            return member.ProxyTags.First();
        }

        private bool IsEscaped(string message) => message.TrimStart().StartsWith(EscapeString);
        
        public struct AutoproxyContext
        {
            public CachedAccount Account;
            public string Content;
            public AutoproxyMode Mode;
            public int? AutoproxyMember;
            public ulong SenderId;
            public ulong GuildId;
        }
    }
}