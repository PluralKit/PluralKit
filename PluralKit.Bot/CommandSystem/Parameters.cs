using Humanizer;
using Myriad.Types;
using Myriad.Extensions;
using PluralKit.Core;
using uniffi.commands;

namespace PluralKit.Bot;

// corresponds to the ffi Paramater type, but with stricter types (also avoiding exposing ffi types!)
public abstract record Parameter()
{
    public record MemberRef(PKMember member): Parameter;
    public record MemberRefs(List<PKMember> members): Parameter;
    public record GroupRef(PKGroup group): Parameter;
    public record GroupRefs(List<PKGroup> groups): Parameter;
    public record SystemRef(PKSystem system): Parameter;
    public record UserRef(User user): Parameter;
    public record MessageRef(Message.Reference message): Parameter;
    public record ChannelRef(Channel channel): Parameter;
    public record GuildRef(Guild guild): Parameter;
    public record MemberPrivacyTarget(MemberPrivacySubject target): Parameter;
    public record GroupPrivacyTarget(GroupPrivacySubject target): Parameter;
    public record SystemPrivacyTarget(SystemPrivacySubject target): Parameter;
    public record PrivacyLevel(Core.PrivacyLevel level): Parameter;
    public record Toggle(bool value): Parameter;
    public record Opaque(string value): Parameter;
    public record Number(int value): Parameter;
    public record Avatar(ParsedImage avatar): Parameter;
    public record ProxySwitchAction(SystemConfig.ProxySwitchAction action): Parameter;
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

    public static string GetRelatedCommands(string prefix, string subject)
    {
        return CommandsMethods.GetRelatedCommands(prefix, subject);
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
        var byId = HasFlag("id", "by-id"); // this is added as a hidden flag to all command definitions
        switch (ffi_param)
        {
            case uniffi.commands.Parameter.MemberRef memberRef:
                return new Parameter.MemberRef(
                    await ctx.ParseMember(memberRef.member, byId)
                    ?? throw new PKError(ctx.CreateNotFoundError("Member", memberRef.member, byId))
                );
            case uniffi.commands.Parameter.MemberRefs memberRefs:
                return new Parameter.MemberRefs(
                    await memberRefs.members.ToAsyncEnumerable().SelectAwait(async m =>
                        await ctx.ParseMember(m, byId)
                        ?? throw new PKError(ctx.CreateNotFoundError("Member", m, byId))
                    ).ToListAsync()
                );
            case uniffi.commands.Parameter.GroupRef groupRef:
                return new Parameter.GroupRef(
                    await ctx.ParseGroup(groupRef.group, byId)
                    ?? throw new PKError(ctx.CreateNotFoundError("Group", groupRef.group))
                );
            case uniffi.commands.Parameter.GroupRefs groupRefs:
                return new Parameter.GroupRefs(
                    await groupRefs.groups.ToAsyncEnumerable().SelectAwait(async g =>
                        await ctx.ParseGroup(g, byId)
                        ?? throw new PKError(ctx.CreateNotFoundError("Group", g, byId))
                    ).ToListAsync()
                );
            case uniffi.commands.Parameter.SystemRef systemRef:
                // todo: do we need byId here?
                return new Parameter.SystemRef(
                    await ctx.ParseSystem(systemRef.system)
                    ?? throw new PKError(ctx.CreateNotFoundError("System", systemRef.system))
                );
            case uniffi.commands.Parameter.UserRef(var userId):
                return new Parameter.UserRef(
                    await ctx.Cache.GetOrFetchUser(ctx.Rest, userId)
                    ?? throw new PKError(ctx.CreateNotFoundError("User", userId.ToString()))
                );
            // todo(dusk): ideally generate enums for these from rust code in the cs glue
            case uniffi.commands.Parameter.MemberPrivacyTarget memberPrivacyTarget:
                // this should never really fail...
                if (!MemberPrivacyUtils.TryParseMemberPrivacy(memberPrivacyTarget.target, out var memberPrivacy))
                    throw new PKError($"Invalid member privacy target {memberPrivacyTarget.target}");
                return new Parameter.MemberPrivacyTarget(memberPrivacy);
            case uniffi.commands.Parameter.GroupPrivacyTarget groupPrivacyTarget:
                // this should never really fail...
                if (!GroupPrivacyUtils.TryParseGroupPrivacy(groupPrivacyTarget.target, out var groupPrivacy))
                    throw new PKError($"Invalid group privacy target {groupPrivacyTarget.target}");
                return new Parameter.GroupPrivacyTarget(groupPrivacy);
            case uniffi.commands.Parameter.SystemPrivacyTarget systemPrivacyTarget:
                // this should never really fail...
                if (!SystemPrivacyUtils.TryParseSystemPrivacy(systemPrivacyTarget.target, out var systemPrivacy))
                    throw new PKError($"Invalid system privacy target {systemPrivacyTarget.target}");
                return new Parameter.SystemPrivacyTarget(systemPrivacy);
            case uniffi.commands.Parameter.PrivacyLevel privacyLevel:
                return new Parameter.PrivacyLevel(privacyLevel.level == "public" ? PrivacyLevel.Public : privacyLevel.level == "private" ? PrivacyLevel.Private : throw new PKError($"Invalid privacy level {privacyLevel.level}"));
            case uniffi.commands.Parameter.ProxySwitchAction(var action):
                SystemConfig.ProxySwitchAction newVal;

                if (action.Equals("off", StringComparison.InvariantCultureIgnoreCase))
                    newVal = SystemConfig.ProxySwitchAction.Off;
                else if (action.Equals("new", StringComparison.InvariantCultureIgnoreCase) || action.Equals("n", StringComparison.InvariantCultureIgnoreCase) || action.Equals("on", StringComparison.InvariantCultureIgnoreCase))
                    newVal = SystemConfig.ProxySwitchAction.New;
                else if (action.Equals("add", StringComparison.InvariantCultureIgnoreCase) || action.Equals("a", StringComparison.InvariantCultureIgnoreCase))
                    newVal = SystemConfig.ProxySwitchAction.Add;
                else
                    throw new PKError("You must pass either \"new\", \"add\", or \"off\" to this command.");

                return new Parameter.ProxySwitchAction(newVal);
            case uniffi.commands.Parameter.Toggle toggle:
                return new Parameter.Toggle(toggle.toggle);
            case uniffi.commands.Parameter.OpaqueString opaque:
                return new Parameter.Opaque(opaque.raw);
            case uniffi.commands.Parameter.OpaqueInt number:
                return new Parameter.Number(number.raw);
            case uniffi.commands.Parameter.Avatar avatar:
                return new Parameter.Avatar(await ctx.GetUserPfp(avatar.avatar) ?? ctx.ParseImage(avatar.avatar));
            case uniffi.commands.Parameter.MessageRef(var guildId, var channelId, var messageId):
                return new Parameter.MessageRef(new Message.Reference(guildId, channelId, messageId));
            case uniffi.commands.Parameter.ChannelRef(var channelId):
                return new Parameter.ChannelRef(await ctx.Rest.GetChannelOrNull(channelId) ?? throw new PKError($"Channel {channelId} not found"));
            case uniffi.commands.Parameter.GuildRef(var guildId):
                return new Parameter.GuildRef(await ctx.Rest.GetGuildOrNull(guildId) ?? throw new PKError($"Guild {guildId} not found"));
            case uniffi.commands.Parameter.Null:
                return null;
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