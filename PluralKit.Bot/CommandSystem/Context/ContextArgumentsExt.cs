using System.Text.RegularExpressions;

using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public static class ContextArgumentsExt
{
    public static string PopArgument(this Context ctx) => throw new PKError("todo: PopArgument");

    public static string PeekArgument(this Context ctx) => throw new PKError("todo: PeekArgument");

    public static string RemainderOrNull(this Context ctx, bool skipFlags = true) => throw new PKError("todo: RemainderOrNull");

    public static bool HasNext(this Context ctx, bool skipFlags = true) => throw new PKError("todo: HasNext");

    public static string FullCommand(this Context ctx) => throw new PKError("todo: FullCommand");

    /// <summary>
    ///     Checks if the next parameter is equal to one of the given keywords and pops it from the stack. Case-insensitive.
    /// </summary>
    public static bool Match(this Context ctx, ref string used, params string[] potentialMatches)
    {
        var arg = ctx.PeekArgument();
        foreach (var match in potentialMatches)
            if (arg.Equals(match, StringComparison.InvariantCultureIgnoreCase))
            {
                used = ctx.PopArgument();
                return true;
            }

        return false;
    }

    /// <summary>
    /// Checks if the next parameter is equal to one of the given keywords. Case-insensitive.
    /// </summary>
    public static bool Match(this Context ctx, params string[] potentialMatches)
    {
        string used = null; // Unused and unreturned, we just yeet it
        return ctx.Match(ref used, potentialMatches);
    }

    /// <summary>
    ///     Checks if the next parameter (starting from `ptr`) is equal to one of the given keywords, and leaves it on the stack. Case-insensitive.
    /// </summary>
    public static bool PeekMatch(this Context ctx, ref int ptr, string[] potentialMatches)
    {
        throw new PKError("todo: PeekMatch");
    }

    /// <summary>
    /// Matches the next *n* parameters against each parameter consecutively.
    /// <br />
    /// Note that this is handled differently than single-parameter Match:
    /// each method parameter is an array of potential matches for the *n*th command string parameter.
    /// </summary>
    public static bool MatchMultiple(this Context ctx, params string[][] potentialParametersMatches)
    {
        throw new PKError("todo: MatchMultiple");
    }

    public static bool MatchFlag(this Context ctx, params string[] potentialMatches)
    {
        // Flags are *ALWAYS PARSED LOWERCASE*. This means we skip out on a "ToLower" call here.
        // Can assume the caller array only contains lowercase *and* the set below only contains lowercase
        throw new NotImplementedException();
    }

    public static bool MatchClear(this Context ctx)
        => ctx.Match("clear", "reset", "default") || ctx.MatchFlag("c", "clear");

    public static ReplyFormat MatchFormat(this Context ctx)
    {
        if (ctx.Match("r", "raw") || ctx.MatchFlag("r", "raw")) return ReplyFormat.Raw;
        if (ctx.Match("pt", "plaintext") || ctx.MatchFlag("pt", "plaintext")) return ReplyFormat.Plaintext;
        return ReplyFormat.Standard;
    }

    public static ReplyFormat PeekMatchFormat(this Context ctx)
    {
        throw new PKError("todo: PeekMatchFormat");
    }

    public static bool MatchToggle(this Context ctx, bool? defaultValue = null)
    {
        var value = ctx.MatchToggleOrNull(defaultValue);
        if (value == null) throw new PKError("You must pass either \"on\" or \"off\" to this command.");
        return value.Value;
    }

    public static bool? MatchToggleOrNull(this Context ctx, bool? defaultValue = null)
    {
        if (defaultValue != null && ctx.MatchClear())
            return defaultValue.Value;

        var yesToggles = new[] { "yes", "on", "enable", "enabled", "true" };
        var noToggles = new[] { "no", "off", "disable", "disabled", "false" };

        if (ctx.Match(yesToggles) || ctx.MatchFlag(yesToggles))
            return true;
        else if (ctx.Match(noToggles) || ctx.MatchFlag(noToggles))
            return false;
        else return null;
    }

    public static (ulong? messageId, ulong? channelId) GetRepliedTo(this Context ctx)
    {
        if (ctx.Message.Type == Message.MessageType.Reply && ctx.Message.MessageReference?.MessageId != null)
            return (ctx.Message.MessageReference.MessageId, ctx.Message.MessageReference.ChannelId);
        return (null, null);
    }

    public static (ulong? messageId, ulong? channelId) ParseMessage(this Context ctx, string maybeMessageRef, bool parseRawMessageId)
    {
        if (parseRawMessageId && ulong.TryParse(maybeMessageRef, out var mid))
            return (mid, null);

        var match = Regex.Match(maybeMessageRef, "https://(?:\\w+.)?discord(?:app)?.com/channels/\\d+/(\\d+)/(\\d+)");
        if (!match.Success)
            return (null, null);

        var channelId = ulong.Parse(match.Groups[1].Value);
        var messageId = ulong.Parse(match.Groups[2].Value);
        return (messageId, channelId);
    }
}

public enum ReplyFormat
{
    Standard,
    Raw,
    Plaintext
}