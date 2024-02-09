#nullable enable
using Myriad.Extensions;
using Myriad.Types;

namespace PluralKit.Bot;

public static class ContextAvatarExt
{
    public static async Task<ParsedImage?> MatchImage(this Context ctx)
    {
        // If we have a user @mention/ID, use their avatar
        if (await ctx.MatchUser() is { } user)
        {
            var url = user.AvatarUrl("png", 256);
            return new ParsedImage { Url = url, Source = AvatarSource.User, SourceUser = user };
        }

        // If we have a positional argument, try to parse it as a URL
        var arg = ctx.RemainderOrNull();
        if (arg != null)
        {
            // Allow surrounding the URL with <angle brackets> to "de-embed"
            if (arg.StartsWith("<") && arg.EndsWith(">"))
                arg = arg.Substring(1, arg.Length - 2);

            if (!Core.MiscUtils.TryMatchUri(arg, out var uri))
                throw Errors.InvalidUrl;

            // ToString URL-decodes, which breaks URLs to spaces; AbsoluteUri doesn't
            return new ParsedImage { Url = uri.AbsoluteUri, Source = AvatarSource.Url };
        }

        // If we have an attachment, use that
        if (ctx.Message.Attachments.FirstOrDefault() is { } attachment)
        {
            // XXX: discord attachment URLs are unable to be validated without their query params
            // keep both the URL with query (for validation) and the clean URL (for storage) around
            var uriBuilder = new UriBuilder(attachment.ProxyUrl);

            ParsedImage img = new ParsedImage { Url = uriBuilder.Uri.AbsoluteUri, Source = AvatarSource.Attachment };
            uriBuilder.Query = "";
            img.CleanUrl = uriBuilder.Uri.AbsoluteUri;
            return img;
        }

        // We should only get here if there are no arguments (which would get parsed as URL + throw if error)
        // and if there are no attachments (which would have been caught just before)
        return null;
    }
}

public struct ParsedImage
{
    public string Url;
    public string? CleanUrl;
    public AvatarSource Source;
    public User? SourceUser;
}

public enum AvatarSource
{
    Url,
    User,
    Attachment
}