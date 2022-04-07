using Microsoft.AspNetCore.Mvc;

using SqlKata;

using Newtonsoft.Json;
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

        var redisClusterInfo = await db.HashGetAllAsync("pluralkit:cluster_stats");
        var clusterInfo = redisClusterInfo.Select(x => JsonConvert.DeserializeObject<ClusterMetricInfo>(x.Value));

        var guildCount = clusterInfo.Sum(x => x.GuildCount);
        var channelCount = clusterInfo.Sum(x => x.ChannelCount);

        var stats = await _repo.GetStats();

        var o = new JObject();
        o.Add("shards", shards.ToJson());
        o.Add("stats", stats.ToJson(guildCount, channelCount));
        o.Add("version", BuildInfoService.FullVersion);

        return Ok(o);
    }

    [HttpPost("bulk_privacy/member")]
    public async Task<IActionResult> BulkMemberPrivacy([FromBody] JObject inner)
    {
        HttpContext.Items.TryGetValue("SystemId", out var systemId);
        if (systemId == null)
            throw Errors.GenericAuthError;

        var data = new JObject();
        data.Add("privacy", inner);

        var patch = MemberPatch.FromJSON(data, APIVersion.V2);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        await _db.ExecuteQuery(patch.Apply(new Query("members").Where("system", systemId)));

        return NoContent();
    }

    [HttpPost("bulk_privacy/group")]
    public async Task<IActionResult> BulkGroupPrivacy([FromBody] JObject inner)
    {
        HttpContext.Items.TryGetValue("SystemId", out var systemId);
        if (systemId == null)
            throw Errors.GenericAuthError;

        var data = new JObject();
        data.Add("privacy", inner);

        var patch = GroupPatch.FromJson(data);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        await _db.ExecuteQuery(patch.Apply(new Query("groups").Where("system", systemId)));

        return NoContent();
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
            if (shard.HasClusterId)
                s.Add("cluster_id", shard.ClusterId);

            o.Add(s);
        }

        return o;
    }
}