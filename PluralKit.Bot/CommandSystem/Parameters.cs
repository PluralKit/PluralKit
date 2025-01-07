using System.Diagnostics;
using PluralKit.Core;
using uniffi.commands;

namespace PluralKit.Bot;

// corresponds to the ffi Paramater type, but with stricter types (also avoiding exposing ffi types!)
public abstract record Parameter()
{
    public record MemberRef(PKMember member): Parameter;
    public record SystemRef(PKSystem system): Parameter;
    public record MemberPrivacyTarget(MemberPrivacySubject target): Parameter;
    public record PrivacyLevel(string level): Parameter;
    public record Toggle(bool value): Parameter;
    public record Opaque(string value): Parameter;
    public record Reset(): Parameter;
}

public class Parameters
{
    private string _cb { get; init; }
    private List<string> _args { get; init; }
    private Dictionary<string, string?> _flags { get; init; }
    private Dictionary<string, uniffi.commands.Parameter> _params { get; init; }

    // just used for errors, temporarily
    public string FullCommand { get; init; }

    public Parameters(string cmd)
    {
        FullCommand = cmd;
        var result = CommandsMethods.ParseCommand(cmd);
        if (result is CommandResult.Ok)
        {
            var command = ((CommandResult.Ok)result).@command;
            _cb = command.@commandRef;
            _args = command.@args;
            _flags = command.@flags;
            _params = command.@params;
        }
        else
        {
            throw new PKError(((CommandResult.Err)result).@error);
        }
    }

    public string Callback()
    {
        return _cb;
    }

    public bool HasFlag(params string[] potentialMatches)
    {
        return potentialMatches.Any(_flags.ContainsKey);
    }

    // resolves a single parameter
    private async Task<Parameter?> ResolveParameter(Context ctx, string param_name)
    {
        if (!_params.ContainsKey(param_name)) return null;
        switch (_params[param_name])
        {
            case uniffi.commands.Parameter.MemberRef memberRef:
                var byId = HasFlag("id", "by-id");
                return new Parameter.MemberRef(
                    await ctx.ParseMember(memberRef.member, byId)
                    ?? throw new PKError(ctx.CreateNotFoundError("Member", memberRef.member, byId))
                );
            case uniffi.commands.Parameter.SystemRef systemRef:
                // todo: do we need byId here?
                return new Parameter.SystemRef(
                    await ctx.ParseSystem(systemRef.system)
                    ?? throw new PKError(ctx.CreateNotFoundError("System", systemRef.system))
                );
            case uniffi.commands.Parameter.MemberPrivacyTarget memberPrivacyTarget:
                // this should never really fail...
                // todo: we shouldn't have *three* different MemberPrivacyTarget types (rust, ffi, c#) syncing the cases will be annoying...
                if (!MemberPrivacyUtils.TryParseMemberPrivacy(memberPrivacyTarget.target, out var target))
                    throw new PKError($"Invalid member privacy target {memberPrivacyTarget.target}");
                return new Parameter.MemberPrivacyTarget(target);
            case uniffi.commands.Parameter.PrivacyLevel privacyLevel:
                return new Parameter.PrivacyLevel(privacyLevel.level);
            case uniffi.commands.Parameter.Toggle toggle:
                return new Parameter.Toggle(toggle.toggle);
            case uniffi.commands.Parameter.OpaqueString opaque:
                return new Parameter.Opaque(opaque.raw);
            case uniffi.commands.Parameter.Reset _:
                return new Parameter.Reset();
        }
        // this should also never happen
        throw new PKError($"Unknown parameter type for parameter {param_name}");
    }

    public async Task<T> ResolveParameter<T>(Context ctx, string param_name, Func<Parameter, T?> extract_func)
    {
        var param = await ResolveParameter(ctx, param_name);
        // todo: i think this should return null for everything...?
        if (param == null) return default;
        return extract_func(param)
            // this should never really happen (hopefully!), but in case the parameter names dont match up (typos...) between rust <-> c#...
            // (it would be very cool to have this statically checked somehow..?)
            ?? throw new PKError($"Parameter {param_name.AsCode()} was not found for command {Callback().AsCode()} -- this is a bug!!");
    }
}