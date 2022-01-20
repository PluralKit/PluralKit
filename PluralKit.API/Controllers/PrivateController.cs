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
    public PrivateController(IServiceProvider svc) : base(svc) { }

    [HttpGet("meta")]
    public async Task<ActionResult<JObject>> Meta()
    {
        var shards = await _repo.GetShards();
        var stats = await _repo.GetStats();

        var o = new JObject();
        o.Add("shards", shards.ToJSON());
        o.Add("stats", stats.ToJson());
        o.Add("version", BuildInfoService.Version);

        return Ok(o);
    }
}