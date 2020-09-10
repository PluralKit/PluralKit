using PluralKit.Core;

namespace PluralKit.Bot
{
    public static class ContextPrivacyExt
    {
        public static PrivacyLevel PopPrivacyLevel(this Context ctx)
        {
            if (ctx.Match("public", "show", "shown", "visible"))
                return PrivacyLevel.Public;

            if (ctx.Match("private", "hide", "hidden"))
                return PrivacyLevel.Private;

            if (!ctx.HasNext())
                throw new PKSyntaxError("You must pass a privacy level (`public` or `private`)");
            
            throw new PKSyntaxError($"Invalid privacy level {ctx.PopArgument().AsCode()} (must be `public` or `private`).");
        }

        public static SystemPrivacySubject PopSystemPrivacySubject(this Context ctx)
        {
            if (!SystemPrivacyUtils.TryParseSystemPrivacy(ctx.PeekArgument(), out var subject))
                throw new PKSyntaxError($"Invalid privacy subject {ctx.PopArgument().AsCode()} (must be `description`, `members`, `front`, `fronthistory`, `groups`, or `all`).");
            
            ctx.PopArgument();
            return subject;
        }
        
        public static MemberPrivacySubject PopMemberPrivacySubject(this Context ctx)
        {
            if (!MemberPrivacyUtils.TryParseMemberPrivacy(ctx.PeekArgument(), out var subject))
                throw new PKSyntaxError($"Invalid privacy subject {ctx.PopArgument().AsCode()} (must be `name`, `description`, `avatar`, `birthday`, `pronouns`, `metadata`, `visibility`, or `all).");
            
            ctx.PopArgument();
            return subject;
        }
        
        public static GroupPrivacySubject PopGroupPrivacySubject(this Context ctx)
        {
            if (!GroupPrivacyUtils.TryParseGroupPrivacy(ctx.PeekArgument(), out var subject))
                throw new PKSyntaxError($"Invalid privacy subject {ctx.PopArgument().AsCode()} (must be `description`, `icon`, `visibility`, or `all).");
            
            ctx.PopArgument();
            return subject;
        }

        public static bool MatchPrivateFlag(this Context ctx, LookupContext pctx)
        {
            var privacy = true;
            if (ctx.MatchFlag("a", "all")) privacy = false;
            if (pctx == LookupContext.ByNonOwner && !privacy) throw Errors.LookupNotAllowed;

            return privacy; 
        }
    }
}