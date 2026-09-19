using PluralKit.Core;

namespace PluralKit.Bot;

public static class JointContextChecksExt
{
    public static JointContext CheckSystemPrivacy(this JointContext ctx, SystemId target, PrivacyLevel level)
    {
        if (level.CanAccess(ctx.DirectLookupContextFor(target))) return ctx;
        throw Errors.LookupNotAllowed;
    }

    public static JointContext CheckGuildContext(this JointContext ctx)
    {
        if (ctx.Guild != null) return ctx;
        throw new PKError("This command can not be run in a DM.");
    }

    public static JointContext CheckDMContext(this JointContext ctx)
    {
        if (ctx.Guild == null) return ctx;
        throw new PKError("This command must be run in a DM.");
    }

    public static JointContext CheckNoSystem(this JointContext ctx)
    {
        if (ctx.System != null)
            throw Errors.ExistingSystemError(ctx.DefaultPrefix);
        return ctx;
    }

    public static JointContext CheckSystem(this JointContext ctx)
    {
        if (ctx.System == null)
            throw Errors.NoSystemError(ctx.DefaultPrefix);
        return ctx;
    }

    public static JointContext CheckSystem(this JointContext ctx, PKSystem system)
    {
        if (system == null)
        {
            // Different errors if user does have a system and was targeting a different 
            // account/id or if they don't have a system. Although this does mishandle 
            // the possibility that an account without a system tries to query a different
            // account without a system
            ctx.CheckSystem();
            throw Errors.NoSystemFoundError();
        }
        return ctx;
    }

    public static JointContext CheckOwnSystem(this JointContext ctx, PKSystem system)
    {
        if (system.Id != ctx.System?.Id)
            throw Errors.NotOwnSystemError;
        return ctx;
    }

    public static JointContext CheckOwnMember(this JointContext ctx, PKMember member)
    {
        if (member.System != ctx.System?.Id)
            throw Errors.NotOwnMemberError;
        return ctx;
    }

    public static JointContext CheckOwnGroup(this JointContext ctx, PKGroup group)
    {
        if (group.System != ctx.System?.Id)
            throw Errors.NotOwnGroupError;
        return ctx;
    }
}