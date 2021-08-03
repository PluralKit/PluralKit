﻿using System.Collections.Generic;
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
                throw new ProxyService.ProxyChecksFailedException("This message was not autoproxied because it starts with a backslash (`\\`).");

            // Find the member we should autoproxy (null if none)
            var member = ctx.AutoproxyMode switch
            {
                AutoproxyMode.Member when ctx.AutoproxyMember != null => 
                    members.FirstOrDefault(m => m.Id == ctx.AutoproxyMember),
                
                AutoproxyMode.Front when ctx.LastSwitchMembers.Length > 0 => 
                    members.FirstOrDefault(m => m.Id == ctx.LastSwitchMembers[0]),
                
                AutoproxyMode.Latch when ctx.LastMessageMember != null =>
                    members.FirstOrDefault(m => m.Id == ctx.LastMessageMember.Value),
                
                _ => null
            };
            // Throw an error if the member is null, message varies depending on autoproxy mode
            if (member == null) 
            {
                if (ctx.AutoproxyMode == AutoproxyMode.Front)
                {
                    throw new ProxyService.ProxyChecksFailedException("You are using autoproxy front with no members switched in, please use `pk;switch <member>` to log a switch with a member.");
                }
                else if (ctx.AutoproxyMode == AutoproxyMode.Member)
                {
                    throw new ProxyService.ProxyChecksFailedException("You are using a member specific autoproxy with an invalid member. Did you delete the member?");
                }
                else if (ctx.AutoproxyMode == AutoproxyMode.Latch)
                {
                    throw new ProxyService.ProxyChecksFailedException("You are using autoproxy latch without any previously sent messages, please send a message using proxy tags first.");
                }
                throw new ProxyService.ProxyChecksFailedException("This message matches none of your proxy tags and autoproxy isn't enabled.");
            } 

            // Moved the IsLatchExpired() check to here, so that an expired latch and a latch without any previous messages throw different errors
            if (ctx.AutoproxyMode == AutoproxyMode.Latch && IsLatchExpired(ctx))
                 throw new ProxyService.ProxyChecksFailedException("Autoproxy latch has expired, please send a message using proxy tags first.");

            if (ctx.AutoproxyMode != AutoproxyMode.Member && !member.AllowAutoproxy)
                throw new ProxyService.ProxyChecksFailedException("This member has autoproxy disabled. To enable it, use `pk;m <member> autoproxy on`.");

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