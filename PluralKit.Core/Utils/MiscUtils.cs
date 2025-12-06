using System.Text.RegularExpressions;

namespace PluralKit.Core;

public static class MiscUtils
{
    // discord mediaproxy URLs used to be stored directly in the database, so now we cleanup image urls before using them outside of proxying
    private static readonly Regex MediaProxyUrl =
        new(
            @"^https?://media.discordapp.net/attachments/(\d{17,19})/(\d{17,19})/([^/\\&\?]+)\.(png|jpg|jpeg|webp)(\?.*)?$");

    private static readonly string DiscordCdnReplacement = "https://cdn.discordapp.com/attachments/$1/$2/$3.$4";

    // Rewrite time "cachebuster" parameters for randomly generated/chosen avatars with custom URLs.
    private static readonly Regex TimePlaceholder = new(@"\{(time(?:stamp|_(?:1m|5m|30m|1h|6h|1d)))\}");
    private const Int64 TimeAccuracy = 60;

    public static bool TryMatchUri(string input, out Uri uri)
    {
        if (input.StartsWith('<') && input.EndsWith('>'))
            input = input[1..^1];

        try
        {
            uri = new Uri(input);
            if (!uri.IsAbsoluteUri || uri.Scheme != "https")
                return false;
        }
        catch (UriFormatException)
        {
            uri = null;
            return false;
        }

        return true;
    }

    public static string? TryGetCleanCdnUrl(this string? url) =>
        url == null ? null : TimePlaceholder.Replace(MediaProxyUrl.Replace(url, DiscordCdnReplacement), ProcessTimePlaceholder);

    private static string? ProcessTimePlaceholder(Match m) {
        // Limit maximum accuracy to avoid too much cache thrashing, multiply for standard-ish Unix time
        // AND with the maximum positive value so it's always positive (as if this code will exist long enough for the 64-bit signed unix time to go negative...)
        var time = ((DateTimeOffset.UtcNow.ToUnixTimeSeconds()/TimeAccuracy)*TimeAccuracy)&Int64.MaxValue;
        
        switch (m.Groups[1].Value) {
            case "timestamp": break;
            case "time_1m": time /= 60; break;
            case "time_5m": time /= 60*5; break;
            case "time_30m": time /= 60*30; break;
            case "time_1h": time /= 60*60; break;
            case "time_6h": time /= 6*60*60; break;
            case "time_1d": time /= 24*60*60; break;
        }

        return time.ToString();
    }

}