using System.Diagnostics;
using Myriad.Types;
using PluralKit.Core;
using uniffi.commands;

namespace PluralKit.Bot;

// corresponds to the ffi Paramater type, but with stricter types (also avoiding exposing ffi types!)
public abstract record Parameter()
{
    public record MemberRef(PKMember member): Parameter;
    public record SystemRef(PKSystem system): Parameter;
    public record GuildRef(Guild guild): Parameter;
    public record MemberPrivacyTarget(MemberPrivacySubject target): Parameter;
    public record PrivacyLevel(string level): Parameter;
    public record Toggle(bool value): Parameter;
    public record Opaque(string value): Parameter;
    public record Avatar(ParsedImage avatar): Parameter;
}

public class Parameters
{
    private string _cb { get; init; }
    private Dictionary<string, uniffi.commands.Parameter?> _flags { get; init; }
    private Dictionary<string, uniffi.commands.Parameter> _params { get; init; }

    // just used for errors, temporarily
    public string FullCommand { get; init; }

    public Parameters(string prefix, string cmd)
    {
        FullCommand = cmd;
        var result = CommandsMethods.ParseCommand(prefix, cmd);
        if (result is CommandResult.Ok)
        {
            var command = ((CommandResult.Ok)result).@command;
            _cb = command.@commandRef;
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

    private async Task<Parameter?> ResolveFfiParam(Context ctx, uniffi.commands.Parameter ffi_param)
    {
        switch (ffi_param)
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
            case uniffi.commands.Parameter.Avatar avatar:
                return new Parameter.Avatar(await ctx.GetUserPfp(avatar.avatar) ?? ctx.ParseImage(avatar.avatar));
            case uniffi.commands.Parameter.GuildRef guildRef:
                return new Parameter.GuildRef(await ctx.ParseGuild(guildRef.guild) ?? throw new PKError($"Guild {guildRef.guild} not found"));
        }
        return null;
    }

    // resolves a single flag with value
    private async Task<Parameter?> ResolveFlag(Context ctx, string flag_name)
    {
        if (!HasFlag(flag_name)) return null;
        var flag_value = _flags[flag_name];
        if (flag_value == null) return null;
        var resolved = await ResolveFfiParam(ctx, flag_value);
        if (resolved != null) return resolved;
        // this should never happen, types are handled rust side
        return null;
    }

    // resolves a single parameter
    private async Task<Parameter?> ResolveParameter(Context ctx, string param_name)
    {
        if (!_params.ContainsKey(param_name)) return null;
        var resolved = await ResolveFfiParam(ctx, _params[param_name]);
        if (resolved != null) return resolved;
        // this should never happen, types are handled rust side
        return null;
    }

    public async Task<T?> ResolveFlag<T>(Context ctx, string flag_name, Func<Parameter, T?> extract_func)
    {
        var param = await ResolveFlag(ctx, flag_name);
        // todo: i think this should return null for everything...?
        if (param == null) return default;
        return extract_func(param)
            // this should never happen unless codegen somehow uses a wrong name
            ?? throw new PKError($"Flag {flag_name.AsCode()} was not found or did not have a value defined for command {Callback().AsCode()} -- this is a bug!!");
    }

    public async Task<T> ResolveParameter<T>(Context ctx, string param_name, Func<Parameter, T?> extract_func)
    {
        var param = await ResolveParameter(ctx, param_name);
        // todo: i think this should return null for everything...?
        if (param == null) return default;
        return extract_func(param)
            // this should never happen unless codegen somehow uses a wrong name
            ?? throw new PKError($"Parameter {param_name.AsCode()} was not found for command {Callback().AsCode()} -- this is a bug!!");
    }
}