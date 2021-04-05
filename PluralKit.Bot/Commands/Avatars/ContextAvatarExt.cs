#nullable enable
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Myriad.Extensions;
using Myriad.Types;

namespace PluralKit.Bot
{
    public static class ContextAvatarExt
    {
        // Rewrite cdn.discordapp.com URLs to media.discordapp.net for jpg/png files
        // This lets us add resizing parameters to "borrow" their media proxy server to downsize the image
        // which in turn makes it more likely to be underneath the size limit!
        private static readonly Regex DiscordCdnUrl = new Regex(@"^https?://(?:cdn\.discordapp\.com|media\.discordapp\.net)/attachments/(\d{17,19})/(\d{17,19})/([^/\\&\?]+)\.(png|jpg|jpeg|webp)(\?.*)?$");
        private static readonly string DiscordMediaUrlReplacementWithCrop = "https://media.discordapp.net/attachments/$1/$2/$3.$4?width=256&height=256";
        private static readonly string DiscordMediaUrlReplacement = "https://media.discordapp.net/attachments/$1/$2/$3.$4";
        
        public static async Task<ParsedImage?> MatchImage(this Context ctx, bool isFullSizeImage = false)
        {
            // If we have a user @mention/ID, use their avatar 
            if (await ctx.MatchUser() is { } user)
            {
                var url = user.AvatarUrl("png", 256);
                return new ParsedImage {Url = url, Source = AvatarSource.User, SourceUser = user};
            }
            
            // If we have a positional argument, try to parse it as a URL
            var arg = ctx.RemainderOrNull();
            if (arg != null)
            {
                // Allow surrounding the URL with <angle brackets> to "de-embed"
                if (arg.StartsWith("<") && arg.EndsWith(">"))
                    arg = arg.Substring(1, arg.Length - 2);
                
                if (!Uri.TryCreate(arg, UriKind.Absolute, out var uri))
                    throw Errors.InvalidUrl(arg);

                if (uri.Scheme != "http" && uri.Scheme != "https")
                    throw Errors.InvalidUrl(arg);
                
                // ToString URL-decodes, which breaks URLs to spaces; AbsoluteUri doesn't
                return new ParsedImage {Url = TryRewriteCdnUrl(uri.AbsoluteUri, isFullSizeImage), Source = AvatarSource.Url};
            }
            
            // If we have an attachment, use that 
            if (ctx.Message.Attachments.FirstOrDefault() is {} attachment)
            {
                var url = TryRewriteCdnUrl(attachment.ProxyUrl, isFullSizeImage);
                return new ParsedImage {Url = url, Source = AvatarSource.Attachment};
            }
            
            // We should only get here if there are no arguments (which would get parsed as URL + throw if error)
            // and if there are no attachments (which would have been caught just before)
            return null;
        }

        private static string TryRewriteCdnUrl(string url, bool isFullSizeImage) =>
            DiscordCdnUrl.Replace(url, isFullSizeImage ? DiscordMediaUrlReplacement : DiscordMediaUrlReplacementWithCrop);
    }

    public struct ParsedImage
    {
        public string Url;
        public AvatarSource Source;
        public User? SourceUser;
    }

    public enum AvatarSource
    {
        Url,
        User,
        Attachment
    }
}
