using NodaTime;

using PluralKit.Core;

namespace PluralKit.Matrix;

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

    public bool TryMatch(MessageContext ctx, AutoproxySettings settings, IReadOnlyCollection<ProxyMember> members,
                         out ProxyMatch match, string messageContent, string prefix,
                         bool hasAttachments, bool allowAutoproxy, bool caseSensitive)
    {
        if (TryMatchTags(members, messageContent, hasAttachments, caseSensitive, out match)) return true;
        if (allowAutoproxy && TryMatchAutoproxy(ctx, settings, members, messageContent, prefix, out match)) return true;
        return false;
    }

    private bool TryMatchTags(IReadOnlyCollection<ProxyMember> members, string messageContent, bool hasAttachments,
                              bool caseSensitive, out ProxyMatch match)
    {
        if (!_parser.TryMatch(members, messageContent, caseSensitive, out match)) return false;

        // Edge case: blank inner text with no attachments — user probably intended something else
        return hasAttachments || match.Content.Trim().Length > 0;
    }

    private bool TryMatchAutoproxy(MessageContext ctx, AutoproxySettings settings, IReadOnlyCollection<ProxyMember> members,
                                   string messageContent, string prefix,
                                   out ProxyMatch match)
    {
        match = default;

        if (!ctx.AllowAutoproxy)
            throw new ProxyChecksFailedException(
                $"Autoproxy is disabled for your account. Type `{prefix} cfg autoproxy account enable` to re-enable it.");

        // Skip autoproxy match if we hit the escape character
        if (messageContent.StartsWith(AutoproxyEscapeCharacter))
            throw new ProxyChecksFailedException(
                "This message was not autoproxied because it starts with a backslash (`\\`).");

        // Find the member we should autoproxy (null if none)
        var member = settings.AutoproxyMode switch
        {
            AutoproxyMode.Member when settings.AutoproxyMember != null =>
                members.FirstOrDefault(m => m.Id == settings.AutoproxyMember),

            AutoproxyMode.Front when ctx.LastSwitchMembers.Length > 0 =>
                members.FirstOrDefault(m => m.Id == ctx.LastSwitchMembers[0]),

            AutoproxyMode.Latch when settings.AutoproxyMember != null =>
                members.FirstOrDefault(m => m.Id == settings.AutoproxyMember.Value),

            _ => null
        };

        if (member == null)
        {
            if (settings.AutoproxyMode == AutoproxyMode.Front)
                throw new ProxyChecksFailedException(
                    $"You are using autoproxy front, but no members are currently registered as fronting. Please use `{prefix} switch <member>` to log a new switch.");
            if (settings.AutoproxyMode == AutoproxyMode.Member)
                throw new ProxyChecksFailedException(
                    "You are using member-specific autoproxy with an invalid member. Was this member deleted?");
            if (settings.AutoproxyMode == AutoproxyMode.Latch)
                throw new ProxyChecksFailedException(
                    "You are using autoproxy latch, but have not sent any messages yet. Please send a message using proxy tags first.");
            throw new ProxyChecksFailedException(
                "This message matches none of your proxy tags and autoproxy is not enabled.");
        }

        if (settings.AutoproxyMode != AutoproxyMode.Member && !member.AllowAutoproxy)
            throw new ProxyChecksFailedException(
                $"This member has autoproxy disabled. To enable it, use `{prefix} m <member> autoproxy on`.");

        if (settings.AutoproxyMode == AutoproxyMode.Latch && IsLatchExpired(ctx, settings))
            throw new ProxyChecksFailedException(
                "Latch-mode autoproxy has timed out. Please send a new message using proxy tags.");

        match = new ProxyMatch
        {
            Content = messageContent,
            Member = member,
            ProxyTags = member.ProxyTags.FirstOrDefault()
        };
        return true;
    }

    private bool IsLatchExpired(MessageContext ctx, AutoproxySettings settings)
    {
        if (ctx.LatchTimeout == 0) return false;

        var timeout = ctx.LatchTimeout.HasValue
            ? Duration.FromSeconds(ctx.LatchTimeout.Value)
            : DefaultLatchExpiryTime;

        return _clock.GetCurrentInstant() - settings.LastLatchTimestamp > timeout;
    }
}

public class ProxyChecksFailedException : Exception
{
    public ProxyChecksFailedException(string message) : base(message) { }
}
