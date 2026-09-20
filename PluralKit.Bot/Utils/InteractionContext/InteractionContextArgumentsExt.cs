namespace PluralKit.Bot;

public static class InteractionContextArgumentsExt
{
    public static ReplyFormat MatchFormat(this InteractionContext ctx) =>
    (ctx.OptionValue("format")?.ToString() ?? "standard") switch
    {
        "raw" => ReplyFormat.Raw,
        "plaintext" => ReplyFormat.Plaintext,
        _ => ReplyFormat.Standard,
    };
}