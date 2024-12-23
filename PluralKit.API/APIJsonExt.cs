using Microsoft.AspNetCore.Http;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API;

public static class APIJsonExt
{
    public static JObject ToJson(this ModelRepository.Counts counts, int guildCount, int channelCount)
    {
        var o = new JObject();

        o.Add("system_count", counts.SystemCount);
        o.Add("member_count", counts.MemberCount);
        o.Add("group_count", counts.GroupCount);
        o.Add("switch_count", counts.SwitchCount);
        o.Add("message_count", counts.MessageCount);

        // Discord statistics
        o.Add("guild_count", guildCount);
        o.Add("channel_count", channelCount);

        return o;
    }

    public static JObject EmbedJson(string title, string type)
    {
        var o = new JObject();

        o.Add("type", "rich");
        o.Add("provider_name", "PluralKit " + type);
        o.Add("provider_url", "https://pluralkit.me");
        o.Add("title", title);

        return o;
    }

    public static async Task WriteJSON(this HttpResponse resp, int statusCode, string jsonText)
    {
        resp.StatusCode = statusCode;
        resp.Headers.Append("content-type", "application/json");
        await resp.WriteAsync(jsonText);
    }
}

public struct FrontersReturnNew
{
    [JsonProperty("id")] public Guid Uuid { get; set; }
    [JsonProperty("timestamp")] public Instant Timestamp { get; set; }
    [JsonProperty("members")] public IEnumerable<JObject> Members { get; set; }
}

public struct SwitchesReturnNew
{
    [JsonProperty("id")] public Guid Uuid { get; set; }
    [JsonProperty("timestamp")] public Instant Timestamp { get; set; }
    [JsonProperty("members")] public IEnumerable<string> Members { get; set; }
}