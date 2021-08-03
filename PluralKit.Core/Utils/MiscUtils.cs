using System;
using System.Text.RegularExpressions;

namespace PluralKit.Core
{
    public static class MiscUtils
    {
        public static bool TryMatchUri(string input, out Uri uri)
        {
            try
            {
                uri = new Uri(input);
                if (!uri.IsAbsoluteUri || (uri.Scheme != "http" && uri.Scheme != "https")) 
                    return false;
            }
            catch (UriFormatException)
            {
                uri = null;
                return false;
            }

            return true;
        }

        // discord mediaproxy URLs used to be stored directly in the database, so now we cleanup image urls before using them outside of proxying
        private static readonly Regex MediaProxyUrl = new Regex(@"^https?://media.discordapp.net/attachments/(\d{17,19})/(\d{17,19})/([^/\\&\?]+)\.(png|jpg|jpeg|webp)(\?.*)?$");
        private static readonly string DiscordCdnReplacement = "https://cdn.discordapp.com/attachments/$1/$2/$3.$4";
        public static string? TryGetCleanCdnUrl(this string? url)
        {
            return url == null ? null : MediaProxyUrl.Replace(url, DiscordCdnReplacement);
        }
    }
}