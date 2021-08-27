using System.Collections.Generic;
using System.Threading.Tasks;

using System.Linq;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}")]
    public class MetaController: ControllerBase
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        public MetaController(IDatabase db, ModelRepository repo)
        {
            _db = db;
            _repo = repo;
        }

        [HttpGet("meta")]
        public async Task<ActionResult<JObject>> GetMeta()
        {
            await using var conn = await _db.Obtain();
            var shards = await _repo.GetShards(conn);

            var o = new JObject();
            o.Add("shards", shards.ToJSON());
            o.Add("version", BuildInfoService.Version);

            return Ok(o);
        }
    }

    public static class MetaJsonExt
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
}