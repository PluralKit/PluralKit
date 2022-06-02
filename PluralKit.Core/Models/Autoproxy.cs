using Newtonsoft.Json.Linq;

using NodaTime;

namespace PluralKit.Core;

public enum AutoproxyMode
{
    Off = 1,
    Front = 2,
    Latch = 3,
    Member = 4
}

public class AutoproxySettings
{
    public AutoproxyMode AutoproxyMode { get; }
    public MemberId? AutoproxyMember { get; }
    public Instant? LastLatchTimestamp { get; }
}

public static class AutoproxyExt
{
    public static JObject ToJson(this AutoproxySettings settings, string? memberHid = null)
    {
        var o = new JObject();

        // tbd
        o.Add("autoproxy_mode", settings.AutoproxyMode.ToString().ToLower());
        o.Add("autoproxy_member", settings.AutoproxyMode == AutoproxyMode.Front ? null : memberHid);
        o.Add("last_latch_timestamp", settings.LastLatchTimestamp?.FormatExport());

        return o;
    }

    public static (AutoproxyMode?, ValidationError?) ParseAutoproxyMode(this JToken o)
    {
        if (o.Type == JTokenType.Null)
            return (AutoproxyMode.Off, null);
        if (o.Type != JTokenType.String)
            return (null, new ValidationError("autoproxy_mode"));

        var value = o.Value<string>();

        switch (value)
        {
            case "off":
                return (AutoproxyMode.Off, null);
            case "front":
                return (AutoproxyMode.Front, null);
            case "latch":
                return (AutoproxyMode.Latch, null);
            case "member":
                return (AutoproxyMode.Member, null);
            default:
                return (null,
                    new ValidationError("autoproxy_mode", $"Value '{value}' is not a valid autoproxy mode."));
        }
    }
}