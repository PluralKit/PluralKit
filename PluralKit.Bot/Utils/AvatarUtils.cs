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
        
    // Rewrite time "cachebuster" parameters for randomly generated/chosen avatars with custom URLs.
    // Number match uses `[1-9][0-9]{0,6}` rather than `[0-9]+` to avoid needing to deal with special cases for zero and limit to reasonable numbers.
    private static readonly Regex TimePlaceholder = new(@"\{time(?:/(?<divisor>[1-9][0-9]{0,6}))?(?:%(?<modulus>[1-9][0-9]{0,6}))?\}", RegexOptions.IgnoreCase);

    public static string? TryRewriteCdnUrl(string? url)
    {
        if (url == null)
            return null;

        var match = DiscordCdnUrl.Match(url);
        var newUrl = DiscordCdnUrl.Replace(url, DiscordMediaUrlReplacement);
        if (match.Groups["query"].Success)
            newUrl += "&" + match.Groups["query"].Value;

        newUrl = TimePlaceholder.Replace(newUrl, ProcessTimePlaceholder);

        return newUrl;
    }
    
    private static string? ProcessTimePlaceholder(Match m) {
        // Minutes are the maximum accuracy to avoid too much cache thrashing
        // AND with the maximum positive value so it's always positive (as if this code will exist long enough for the 64-bit signed unix time to go negative...)
        var time = (DateTimeOffset.UtcNow.ToUnixTimeSeconds()/60)&Int64.MaxValue;

        if (m.Groups["divisor"].Success)
            time /= Int32.Parse(m.Groups["divisor"].Value); // as above - guaranteed to not throw and be > 0

        if (m.Groups["modulus"].Success)
            time %= Int32.Parse(m.Groups["modulus"].Value);

        return time.ToString();
    }

    public static bool IsDiscordCdnUrl(string? url) => url != null && DiscordCdnUrl.Match(url).Success;
}