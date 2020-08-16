using DSharpPlus;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public static class ContextChecksExt
    {
        public static Context CheckGuildContext(this Context ctx)
        {
            if (ctx.Channel.Guild != null) return ctx;
            throw new PKError("This command can not be run in a DM.");
        }
        
        public static Context CheckSystemPrivacy(this Context ctx, PKSystem target, PrivacyLevel level)
        {
            if (level.CanAccess(ctx.LookupContextFor(target))) return ctx;
            throw new PKError("You do not have permission to access this information.");
        }
        
        public static Context CheckOwnMember(this Context ctx, PKMember member)
        {
            if (member.System != ctx.System?.Id)
                throw Errors.NotOwnMemberError;
            return ctx;
        }
        
        public static Context CheckOwnGroup(this Context ctx, PKGroup group)
        {
            if (group.System != ctx.System?.Id)
                throw Errors.NotOwnGroupError;
            return ctx;
        }
        
        public static Context CheckSystem(this Context ctx)
        {
            if (ctx.System == null)
                throw Errors.NoSystemError;
            return ctx;
        }

        public static Context CheckNoSystem(this Context ctx)
        {
            if (ctx.System != null)
                throw Errors.ExistingSystemError;
            return ctx;
        }
        
        public static Context CheckAuthorPermission(this Context ctx, Permissions neededPerms, string permissionName)
        {
            // TODO: can we always assume Author is a DiscordMember? I would think so, given they always come from a
            // message received event...
            var hasPerms = ctx.Channel.PermissionsInSync(ctx.Author);
            if ((hasPerms & neededPerms) != neededPerms)
                throw new PKError($"You must have the \"{permissionName}\" permission in this server to use this command.");
            return ctx;
        }
    }
}