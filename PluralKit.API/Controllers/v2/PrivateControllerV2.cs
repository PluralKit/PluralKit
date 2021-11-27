using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

namespace PluralKit.API;

[ApiController]
[ApiVersion("2.0")]
[Route("v{version:apiVersion}")]
public class PrivateControllerV2: PKControllerBase
{
    public PrivateControllerV2(IServiceProvider svc) : base(svc) { }

    [HttpGet("meta")]
    public async Task<ActionResult<JObject>> Meta()
    {
        var shards = await _repo.GetShards();
        var stats = await _repo.GetStats();

        var o = new JObject();
        o.Add("shards", shards.ToJSON());
        o.Add("stats", stats.ToJson());

        return Ok(o);
    }
}