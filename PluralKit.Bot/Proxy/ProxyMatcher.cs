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

        public bool TryMatch(IReadOnlyCollection<ProxyMember> members, out ProxyMatch match, string messageContent,
                             bool hasAttachments, bool allowAutoproxy)
        {
            if (TryMatchTags(members, messageContent, hasAttachments, out match)) return true;
            if (allowAutoproxy && TryMatchAutoproxy(members, messageContent, out match)) return true;
            return false;
        }

        private bool TryMatchTags(IReadOnlyCollection<ProxyMember> members, string messageContent, bool hasAttachments, out ProxyMatch match)
        {
            if (!_parser.TryMatch(members, messageContent, out match)) return false;
            
            // Edge case: If we got a match with blank inner text, we'd normally just send w/ attachments
            // However, if there are no attachments, the user probably intended something else, so we "un-match" and proceed to autoproxy
            return hasAttachments || match.Content.Length > 0;
        }

        private bool TryMatchAutoproxy(IReadOnlyCollection<ProxyMember> members, string messageContent,
                                       out ProxyMatch match)
        {
            match = default;

            // We handle most autoproxy logic in the database function, so we just look for the member that's marked
            var info = members.FirstOrDefault(i => i.IsAutoproxyMember);
            if (info == null) return false;

            // If we're in latch mode and the latch message is too old, fail the match too
            if (info.AutoproxyMode == AutoproxyMode.Latch && info.LatchMessage != null)
            {
                var timestamp = DiscordUtils.SnowflakeToInstant(info.LatchMessage.Value);
                if (_clock.GetCurrentInstant() - timestamp > LatchExpiryTime) return false;
            }

            // Match succeeded, build info object and return
            match = new ProxyMatch
            {
                Content = messageContent,
                Member = info,

                // We're autoproxying, so not using any proxy tags here
                // we just find the first pair of tags (if any), otherwise null
                ProxyTags = info.ProxyTags.FirstOrDefault()
            };
            return true;
        }
    }
}