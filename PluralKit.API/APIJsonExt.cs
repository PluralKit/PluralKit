using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API
{
    public static class APIJsonExt
    {
        public static JArray ToJSON(this IEnumerable<PKShardInfo> shards)
        {
            var o = new JArray();

            foreach (var shard in shards)
            {
                var s = new JObject();
                s.Add("id", shard.Id);

                if (shard.Status == PKShardInfo.ShardStatus.Down)
                    s.Add("status", "down");
                else
                    s.Add("status", "up");

                s.Add("ping", shard.Ping);
                s.Add("last_heartbeat", shard.LastHeartbeat.ToString());
                s.Add("last_connection", shard.LastConnection.ToString());

                o.Add(s);
            }

            return o;
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

}