using PluralKit.Core;
using uniffi.commands;

namespace PluralKit.Bot;

public class Parameters
{
    private string _cb { get; init; }
    private List<string> _args { get; init; }
    private Dictionary<string, string?> _flags { get; init; }
    private Dictionary<string, Parameter> _params { get; init; }

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

    public async Task<ResolvedParameters> ResolveParameters(Context ctx)
    {
        var parsed_members = await MemberParams().ToAsyncEnumerable().ToDictionaryAwaitAsync(async item => item.Key, async item =>
            await ctx.ParseMember(this, item.Value) ?? throw new PKError(ctx.CreateNotFoundError(this, "Member", item.Value))
        );
        var parsed_systems = await SystemParams().ToAsyncEnumerable().ToDictionaryAwaitAsync(async item => item.Key, async item =>
            await ctx.ParseSystem(item.Value) ?? throw new PKError(ctx.CreateNotFoundError(this, "System", item.Value))
        );
        return new ResolvedParameters(this, parsed_members, parsed_systems);
    }

    public string Callback()
    {
        return _cb;
    }

    public IDictionary<string, string> Flags()
    {
        return _flags;
    }

    private Dictionary<string, string> Params(Func<ParameterKind, bool> filter)
    {
        return _params.Where(item => filter(item.Value.@kind)).ToDictionary(item => item.Key, item => item.Value.@raw);
    }

    public IDictionary<string, string> Params()
    {
        return Params(_ => true);
    }

    public IDictionary<string, string> MemberParams()
    {
        return Params(kind => kind == ParameterKind.MemberRef);
    }

    public IDictionary<string, string> SystemParams()
    {
        return Params(kind => kind == ParameterKind.SystemRef);
    }
}

// TODO: im not really sure if this should be the way to go
public class ResolvedParameters
{
    public readonly Parameters Raw;
    public readonly Dictionary<string, PKMember> MemberParams;
    public readonly Dictionary<string, PKSystem> SystemParams;

    public ResolvedParameters(Parameters parameters, Dictionary<string, PKMember> member_params, Dictionary<string, PKSystem> system_params)
    {
        Raw = parameters;
        MemberParams = member_params;
        SystemParams = system_params;
    }
}

// TODO: move this to another file (?)
public static class ParametersExt
{
    public static bool HasFlag(this Parameters parameters, params string[] potentialMatches)
    {
        return potentialMatches.Any(parameters.Flags().ContainsKey);
    }
}
