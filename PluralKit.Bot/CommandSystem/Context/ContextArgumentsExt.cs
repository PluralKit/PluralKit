using System.Text.RegularExpressions;

using Myriad.Types;

namespace PluralKit.Bot;

public static class ContextArgumentsExt
{
    public static Message.Reference? GetRepliedTo(this Context ctx)
    {
        if (ctx.Message.Type == Message.MessageType.Reply && ctx.Message.MessageReference?.MessageId != null)
            return ctx.Message.MessageReference;
        return null;
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