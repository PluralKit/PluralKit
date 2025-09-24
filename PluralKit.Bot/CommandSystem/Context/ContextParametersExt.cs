using PluralKit.Core;

namespace PluralKit.Bot;

public static class ContextParametersExt
{
    public static async Task<string?> ParamResolveOpaque(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveParameter(
            ctx, param_name,
            param => (param as Parameter.Opaque)?.value
        );
    }

    public static async Task<PKMember?> ParamResolveMember(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveParameter(
            ctx, param_name,
            param => (param as Parameter.MemberRef)?.member
        );
    }

    public static async Task<List<PKMember>> ParamResolveMembers(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveParameter(
            ctx, param_name,
            param => (param as Parameter.MemberRefs)?.members
        );
    }

    public static async Task<PKSystem?> ParamResolveSystem(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveParameter(
            ctx, param_name,
            param => (param as Parameter.SystemRef)?.system
        );
    }

    public static async Task<MemberPrivacySubject?> ParamResolveMemberPrivacyTarget(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveParameter(
            ctx, param_name,
            param => (param as Parameter.MemberPrivacyTarget)?.target
        );
    }

    public static async Task<SystemPrivacySubject?> ParamResolveSystemPrivacyTarget(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveParameter(
            ctx, param_name,
            param => (param as Parameter.SystemPrivacyTarget)?.target
        );
    }

    public static async Task<PrivacyLevel?> ParamResolvePrivacyLevel(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveParameter(
            ctx, param_name,
            param => (param as Parameter.PrivacyLevel)?.level
        );
    }

    public static async Task<bool?> ParamResolveToggle(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveParameter(
            ctx, param_name,
            param => (param as Parameter.Toggle)?.value
        );
    }

    public static async Task<ParsedImage?> ParamResolveAvatar(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveParameter(
            ctx, param_name,
            param => (param as Parameter.Avatar)?.avatar
        );
    }

    public static async Task<Myriad.Types.Guild?> ParamResolveGuild(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveParameter(
            ctx, param_name,
            param => (param as Parameter.GuildRef)?.guild
        );
    }
}