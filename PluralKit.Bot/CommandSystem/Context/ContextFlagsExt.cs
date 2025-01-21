using PluralKit.Core;

namespace PluralKit.Bot;

public static class ContextFlagsExt
{
    public static async Task<string?> FlagResolveOpaque(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveFlag(
            ctx, param_name,
            param => (param as Parameter.Opaque)?.value
        );
    }

    public static async Task<PKMember?> FlagResolveMember(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveFlag(
            ctx, param_name,
            param => (param as Parameter.MemberRef)?.member
        );
    }

    public static async Task<PKSystem?> FlagResolveSystem(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveFlag(
            ctx, param_name,
            param => (param as Parameter.SystemRef)?.system
        );
    }

    public static async Task<MemberPrivacySubject?> FlagResolveMemberPrivacyTarget(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveFlag(
            ctx, param_name,
            param => (param as Parameter.MemberPrivacyTarget)?.target
        );
    }

    public static async Task<string?> FlagResolvePrivacyLevel(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveFlag(
            ctx, param_name,
            param => (param as Parameter.PrivacyLevel)?.level
        );
    }

    public static async Task<bool?> FlagResolveToggle(this Context ctx, string param_name)
    {
        return await ctx.Parameters.ResolveFlag(
            ctx, param_name,
            param => (param as Parameter.Toggle)?.value
        );
    }
}