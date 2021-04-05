using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using PluralKit.Core;

using SixLabors.ImageSharp;

namespace PluralKit.Bot {
    public static class AvatarUtils {
        public static async Task VerifyAvatarOrThrow(string url, bool isFullSizeImage = false)
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
                Uri uri;
                try
                {
                    uri = new Uri(url);
                    if (!uri.IsAbsoluteUri || (uri.Scheme != "http" && uri.Scheme != "https")) 
                        throw Errors.InvalidUrl(url);
                }
                catch (UriFormatException)
                {
                    throw Errors.InvalidUrl(url);
                }

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
                // Check image size (only if it's an avatar)
                if ((image.Width > Limits.AvatarDimensionLimit || image.Height > Limits.AvatarDimensionLimit) && !isFullSizeImage)
                    throw Errors.AvatarDimensionsTooLarge(image.Width, image.Height);
            }
        }
    }
}