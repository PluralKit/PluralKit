using PluralKit.Core;

namespace PluralKit.Bot;

public static class ContextPrivacyExt
{
    public static PrivacyLevel PopPrivacyLevel(this Context ctx)
    {
        if (ctx.Match("public", "show", "shown", "visible"))
            return PrivacyLevel.Public;

        if (ctx.Match("private", "hide", "hidden"))
            return PrivacyLevel.Private;

        if (ctx.Match("trusted", "authorized", "whitelisted", "trusted-only"))
            return PrivacyLevel.Trusted;

        if (!ctx.HasNext())
            throw new PKSyntaxError("You must pass a privacy level (`public`, `private`, or `trusted`)");

        throw new PKSyntaxError(
            $"Invalid privacy level {ctx.PopArgument().AsCode()} (must be `public`, `private`, or `trusted`).");
    }

    public static SystemPrivacySubject PopSystemPrivacySubject(this Context ctx)
    {
        if (!SystemPrivacyUtils.TryParseSystemPrivacy(ctx.PeekArgument(), out var subject))
            throw new PKSyntaxError(
                $"Invalid privacy subject {ctx.PopArgument().AsCode()} (must be `description`, `members`, `front`, `fronthistory`, `groups`, or `all`).");

        ctx.PopArgument();
        return subject;
    }

    public static MemberPrivacySubject PopMemberPrivacySubject(this Context ctx)
    {
        if (!MemberPrivacyUtils.TryParseMemberPrivacy(ctx.PeekArgument(), out var subject))
            throw new PKSyntaxError(
                $"Invalid privacy subject {ctx.PopArgument().AsCode()} (must be `name`, `description`, `avatar`, `birthday`, `pronouns`, `metadata`, `visibility`, or `all`).");

        ctx.PopArgument();
        return subject;
    }

    public static GroupPrivacySubject PopGroupPrivacySubject(this Context ctx)
    {
        if (!GroupPrivacyUtils.TryParseGroupPrivacy(ctx.PeekArgument(), out var subject))
            throw new PKSyntaxError(
                $"Invalid privacy subject {ctx.PopArgument().AsCode()} (must be `name`, `description`, `icon`, `metadata`, `visibility`, or `all`).");

        ctx.PopArgument();
        return subject;
    }

    public static PrivacyFilter GetPrivacyFilter(this Context ctx, LookupContext dlCtx)
    {
        var privacyFilter = PrivacyFilter.Public;
        if (ctx.MatchFlag("a", "all"))
        {
            switch (dlCtx)
            {
                case LookupContext.ByOwner:
                    privacyFilter = 0;
                    break;
                case LookupContext.ByTrusted:
                    privacyFilter = PrivacyFilter.Public | PrivacyFilter.Trusted;
                    break;
                default:
                    throw Errors.LookupNotAllowed;
            }
        }
        else if (ctx.MatchFlag("po", "private-only"))
        {
            switch (dlCtx)
            {
                case LookupContext.ByOwner:
                    privacyFilter = PrivacyFilter.Private;
                    break;
                case LookupContext.ByTrusted:
                    privacyFilter = PrivacyFilter.Trusted;
                    break;
                default:
                    throw Errors.LookupNotAllowed;
            }
        }
        else if (ctx.MatchFlag("to", "trusted-only"))
        {
            switch (dlCtx)
            {
                case LookupContext.ByOwner:
                case LookupContext.ByTrusted:
                    privacyFilter = PrivacyFilter.Trusted;
                    break;
                default:
                    throw Errors.LookupNotAllowed;
            }
        }
        else if (ctx.MatchFlag("tv", "trusted-view"))
        {
            switch (dlCtx)
            {
                case LookupContext.ByOwner:
                case LookupContext.ByTrusted:
                    privacyFilter = PrivacyFilter.Trusted | PrivacyFilter.Public;
                    break;
                default:
                    throw Errors.LookupNotAllowed;
            }
        }

        return privacyFilter;
    }
}