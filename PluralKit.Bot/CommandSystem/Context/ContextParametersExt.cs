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

    public static async Task<string?> ParamResolvePrivacyLevel(this Context ctx, string param_name)
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
}