using Autofac;

using Myriad.Extensions;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public static class ContextChecksExt
{
    public static Context CheckGuildContext(this Context ctx)
    {
        if (ctx.Channel.GuildId != null) return ctx;
        throw new PKError("This command can not be run in a DM.");
    }

    public static Context CheckDMContext(this Context ctx)
    {
        if (ctx.Channel.GuildId == null) return ctx;
        throw new PKError("This command must be run in a DM.");
    }

    public static Context CheckSystemPrivacy(this Context ctx, SystemId target, PrivacyLevel level)
    {
        if (level.CanAccess(ctx.DirectLookupContextFor(target))) return ctx;
        throw Errors.LookupNotAllowed;
    }

    public static Context CheckOwnSystem(this Context ctx, PKSystem system)
    {
        if (system.Id != ctx.System?.Id)
            throw Errors.NotOwnSystemError;
        return ctx;
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
            throw Errors.NoSystemError(ctx.DefaultPrefix);
        return ctx;
    }

    public static Context CheckSystem(this Context ctx, PKSystem system)
    {
        if (system == null)
            throw Errors.NoSystemError(ctx.DefaultPrefix);
        return ctx;
    }

    public static Context CheckNoSystem(this Context ctx)
    {
        if (ctx.System != null)
            throw Errors.ExistingSystemError(ctx.DefaultPrefix);
        return ctx;
    }

    public static async Task<Context> CheckAuthorPermission(this Context ctx, PermissionSet neededPerms,
                                                            string permissionName)
    {
        if ((await ctx.UserPermissions & neededPerms) != neededPerms)
            throw new PKError(
                $"You must have the \"{permissionName}\" permission in this server to use this command.");
        return ctx;
    }

    public static async Task<bool> CheckPermissionsInGuildChannel(this Context ctx, Channel channel,
                                                                  PermissionSet neededPerms)
    {
        // this is a quick hack, should probably do it properly eventually
        var guild = await ctx.Cache.TryGetGuild(channel.GuildId.Value);
        if (guild == null)
            guild = await ctx.Rest.GetGuild(channel.GuildId.Value);
        if (guild == null)
            return false;

        var guildMember = ctx.Member;

        if (ctx.Guild?.Id != channel.GuildId)
        {
            guildMember = await ctx.Rest.GetGuildMember(channel.GuildId.Value, ctx.Author.Id);
            if (guildMember == null)
                return false;
        }

        var userPermissions = PermissionExtensions.PermissionsFor(guild, channel, ctx.Author.Id, guildMember);
        if ((userPermissions & neededPerms) == 0)
            return false;

        return true;
    }

    public static bool CheckBotAdmin(this Context ctx)
    {
        var botConfig = ctx.Services.Resolve<BotConfig>();
        return botConfig.AdminRole != null && ctx.Member != null &&
               ctx.Member.Roles.Contains(botConfig.AdminRole.Value);
    }

    public static Context AssertBotAdmin(this Context ctx)
    {
        if (!ctx.CheckBotAdmin())
            throw new PKError("This command is only usable by bot admins.");

        return ctx;
    }
}