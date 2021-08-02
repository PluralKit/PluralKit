using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using PluralKit.Core;

using SixLabors.ImageSharp;

namespace PluralKit.Bot {
    public static class AvatarUtils {
        public static async Task VerifyAvatarOrThrow(string url)
        {
            if (url.Length > Limits.MaxUriLength) 
                throw Errors.UrlTooLong(url);

            // List of MIME types we consider acceptable
            var acceptableMimeTypes = new[]
            {
                "image/jpeg",
                "image/gif",
                "image/png"
                // TODO: add image/webp once ImageSharp supports this
            };

            using (var client = new HttpClient())
            {
                if (!PluralKit.Core.MiscUtils.TryMatchUri(url, out var uri))
                    throw Errors.InvalidUrl(url);

                url = TryRewriteCdnUrl(url);

                var response = await client.GetAsync(uri);
                if (!response.IsSuccessStatusCode) // Check status code
                    throw Errors.AvatarServerError(response.StatusCode);
                if (response.Content.Headers.ContentLength == null) // Check presence of content length
                    throw Errors.AvatarNotAnImage(null);
                if (response.Content.Headers.ContentLength > Limits.AvatarFileSizeLimit) // Check content length
                    throw Errors.AvatarFileSizeLimit(response.Content.Headers.ContentLength.Value);
                if (!acceptableMimeTypes.Contains(response.Content.Headers.ContentType.MediaType)) // Check MIME type
                    throw Errors.AvatarNotAnImage(response.Content.Headers.ContentType.MediaType);

                // Parse the image header in a worker
                var stream = await response.Content.ReadAsStreamAsync();
                var image = await Task.Run(() => Image.Identify(stream));
                if (image == null) throw Errors.AvatarInvalid;
                if (image.Width > Limits.AvatarDimensionLimit || image.Height > Limits.AvatarDimensionLimit) // Check image size
                    throw Errors.AvatarDimensionsTooLarge(image.Width, image.Height);
            }
        }

        // Rewrite cdn.discordapp.com URLs to media.discordapp.net for jpg/png files
        // This lets us add resizing parameters to "borrow" their media proxy server to downsize the image
        // which in turn makes it more likely to be underneath the size limit!
        private static readonly Regex DiscordCdnUrl = new Regex(@"^https?://(?:cdn\.discordapp\.com|media\.discordapp\.net)/attachments/(\d{17,19})/(\d{17,19})/([^/\\&\?]+)\.(png|jpg|jpeg|webp)(\?.*)?$");
        private static readonly string DiscordMediaUrlReplacement = "https://media.discordapp.net/attachments/$1/$2/$3.$4?width=256&height=256";
        public static string? TryRewriteCdnUrl(string? url)
        {
            return url == null ? null : DiscordCdnUrl.Replace(url, DiscordMediaUrlReplacement);
        }
    }
}