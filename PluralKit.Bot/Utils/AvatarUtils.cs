using System.Text.RegularExpressions;

using PluralKit.Core;

namespace PluralKit.Bot;

public static class AvatarUtils
{
    // Rewrite cdn.discordapp.com URLs to media.discordapp.net for jpg/png files
    // This lets us add resizing parameters to "borrow" their media proxy server to downsize the image
    // which in turn makes it more likely to be underneath the size limit!
    private static readonly Regex DiscordCdnUrl =
        new(@"^https?://(?:cdn\.discordapp\.com|media\.discordapp\.net)/attachments/(\d{17,19})/(\d{17,19})/([^/\\&\?]+)\.(png|jpe?g|gif|webp)(?:\?(?<query>.*))?$", RegexOptions.IgnoreCase);

    private static readonly string DiscordMediaUrlReplacement =
        "https://media.discordapp.net/attachments/$1/$2/$3.$4?width=256&height=256";

    public static string? TryRewriteCdnUrl(string? url)
    {
        if (url == null)
            return null;

        var match = DiscordCdnUrl.Match(url);
        var newUrl = DiscordCdnUrl.Replace(url, DiscordMediaUrlReplacement);
        if (match.Groups["query"].Success)
            newUrl += "&" + match.Groups["query"].Value;

        return newUrl;
    }

    public static bool IsDiscordCdnUrl(string? url) => url != null && DiscordCdnUrl.Match(url).Success;
}