using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

// Internal API definitions
// I would prefer if you do not use any of these APIs in your own integrations.
// It is unstable and subject to change at any time (which is why it's not versioned)

// If for some reason you do need access to something defined here,
// let us know in #api-support on the support server (https://discord.com/invite/PczBt78) and I'll see if it can be made public

[ApiController]
[Route("private")]
public class PrivateController: PKControllerBase
{
    private readonly RedisService _redis;
    public PrivateController(IServiceProvider svc) : base(svc)
    {
        _redis = svc.GetRequiredService<RedisService>();
    }

    [HttpGet("meta")]
    public async Task<ActionResult<JObject>> Meta()
    {
        var db = _redis.Connection.GetDatabase();
        var redisInfo = await db.HashGetAllAsync("pluralkit:shardstatus");
        var shards = redisInfo.Select(x => Proto.Unmarshal<ShardState>(x.Value)).OrderBy(x => x.ShardId);

        var stats = await _repo.GetStats();

        var o = new JObject();
        o.Add("shards", shards.ToJson());
        o.Add("stats", stats.ToJson());
        o.Add("version", BuildInfoService.FullVersion);

        return Ok(o);
    }
}

public static class PrivateJsonExt
{
    public static JArray ToJson(this IEnumerable<ShardState> shards)
    {
        var o = new JArray();

        foreach (var shard in shards)
        {
            var s = new JObject();
            s.Add("id", shard.ShardId);

            if (!shard.Up)
                s.Add("status", "down");
            else
                s.Add("status", "up");

            s.Add("ping", shard.Latency);
            s.Add("disconnection_count", shard.DisconnectionCount);
            s.Add("last_heartbeat", shard.LastHeartbeat.ToString());
            s.Add("last_connection", shard.LastConnection.ToString());

            o.Add(s);
        }

        return o;
    }
}