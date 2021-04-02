using System.Collections.Generic;
using System.Linq;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class ProxyMatcher
    {
        private static readonly char AutoproxyEscapeCharacter = '\\';
        public static readonly Duration DefaultLatchExpiryTime = Duration.FromHours(6);

        private readonly IClock _clock;
        private readonly ProxyTagParser _parser;

        public ProxyMatcher(ProxyTagParser parser, IClock clock)
        {
            _parser = parser;
            _clock = clock;
        }

        public bool TryMatch(MessageContext ctx, IReadOnlyCollection<ProxyMember> members, out ProxyMatch match, string messageContent,
                             bool hasAttachments, bool allowAutoproxy)
        {
            if (TryMatchTags(members, messageContent, hasAttachments, out match)) return true;
            if (allowAutoproxy && TryMatchAutoproxy(ctx, members, messageContent, out match)) return true;
            return false;
        }

        private bool TryMatchTags(IReadOnlyCollection<ProxyMember> members, string messageContent, bool hasAttachments, out ProxyMatch match)
        {
            if (!_parser.TryMatch(members, messageContent, out match)) return false;
            
            // Edge case: If we got a match with blank inner text, we'd normally just send w/ attachments
            // However, if there are no attachments, the user probably intended something else, so we "un-match" and proceed to autoproxy
            return hasAttachments || match.Content.Trim().Length > 0;
        }

        private bool TryMatchAutoproxy(MessageContext ctx, IReadOnlyCollection<ProxyMember> members, string messageContent,
                                       out ProxyMatch match)
        {
            match = default;

            // Skip autoproxy match if we hit the escape character
            if (messageContent.StartsWith(AutoproxyEscapeCharacter))
                return false;

            // Find the member we should autoproxy (null if none)
            var member = ctx.AutoproxyMode switch
            {
                AutoproxyMode.Member when ctx.AutoproxyMember != null => 
                    members.FirstOrDefault(m => m.Id == ctx.AutoproxyMember),
                
                AutoproxyMode.Front when ctx.LastSwitchMembers.Length > 0 => 
                    members.FirstOrDefault(m => m.Id == ctx.LastSwitchMembers[0]),
                
                AutoproxyMode.Latch when ctx.AutoproxyMember != null => // && !IsLatchExpired(ctx) =>
                    members.FirstOrDefault(m => m.Id == ctx.AutoproxyMember.Value),
                
                _ => null
            };

            if (member == null || (ctx.AutoproxyMode != AutoproxyMode.Member && !member.AllowAutoproxy)) return false;
            match = new ProxyMatch
            {
                Content = messageContent,
                Member = member,
            
                // We're autoproxying, so not using any proxy tags here
                // we just find the first pair of tags (if any), otherwise null
                ProxyTags = member.ProxyTags.FirstOrDefault()
            };
            return true;
        }

        private bool IsLatchExpired(MessageContext ctx)
        {
            if (ctx.LastMessage == null) return true;
            if (ctx.LatchTimeout == 0) return false;

            var timeout = ctx.LatchTimeout.HasValue
                ? Duration.FromSeconds(ctx.LatchTimeout.Value) 
                : DefaultLatchExpiryTime;

            var timestamp = DiscordUtils.SnowflakeToInstant(ctx.LastMessage.Value);
            return _clock.GetCurrentInstant() - timestamp > timeout;
        }
    }
}