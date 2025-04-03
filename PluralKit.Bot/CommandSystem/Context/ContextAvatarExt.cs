#nullable enable
using Myriad.Extensions;
using Myriad.Types;

namespace PluralKit.Bot;

public static class ContextAvatarExt
{
    public static ParsedImage? ExtractImageFromAttachment(this Context ctx)
    {
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
    public static async Task<ParsedImage?> GetUserPfp(this Context ctx, string arg)
    {
        // If we have a user @mention/ID, use their avatar
        if (await ctx.ParseUser(arg) is { } user)
        {
            var url = user.AvatarUrl("png", 256);
            return new ParsedImage { Url = url, Source = AvatarSource.User, SourceUser = user };
        }

        return null;
    }
    public static ParsedImage ParseImage(this Context ctx, string arg)
    {
        // Allow surrounding the URL with <angle brackets> to "de-embed"
        if (arg.StartsWith("<") && arg.EndsWith(">"))
            arg = arg.Substring(1, arg.Length - 2);

        if (!Core.MiscUtils.TryMatchUri(arg, out var uri))
            throw Errors.InvalidUrl;

        // ToString URL-decodes, which breaks URLs to spaces; AbsoluteUri doesn't
        return new ParsedImage { Url = uri.AbsoluteUri, Source = AvatarSource.Url };
    }
    public static async Task<ParsedImage?> MatchImage(this Context ctx)
    {
        throw new NotImplementedException();
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
    Attachment,
    HostedCdn
}