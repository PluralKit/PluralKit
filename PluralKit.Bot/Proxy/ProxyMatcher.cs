using System.Collections.Generic;
using System.Linq;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class ProxyMatcher
    {
        public static readonly Duration LatchExpiryTime = Duration.FromHours(6);

        private IClock _clock;
        private ProxyTagParser _parser;

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
            return hasAttachments || match.Content.Length > 0;
        }

        private bool TryMatchAutoproxy(MessageContext ctx, IReadOnlyCollection<ProxyMember> members, string messageContent,
                                       out ProxyMatch match)
        {
            match = default;

            // Find the member we should autoproxy (null if none)
            var member = ctx.AutoproxyMode switch
            {
                AutoproxyMode.Member when ctx.AutoproxyMember != null => 
                    members.FirstOrDefault(m => m.Id == ctx.AutoproxyMember),
                
                AutoproxyMode.Front when ctx.LastSwitchMembers.Count > 0 => 
                    members.FirstOrDefault(m => m.Id == ctx.LastSwitchMembers[0]),
                
                AutoproxyMode.Latch when ctx.LastMessageMember != null && !IsLatchExpired(ctx.LastMessage) =>
                    members.FirstOrDefault(m => m.Id == ctx.LastMessageMember.Value),
                
                _ => null
            };

            if (member == null) return false;
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

        private bool IsLatchExpired(ulong? messageId)
        {
            if (messageId == null) return true;
            var timestamp = DiscordUtils.SnowflakeToInstant(messageId.Value);
            return _clock.GetCurrentInstant() - timestamp > LatchExpiryTime;
        }
    }
}