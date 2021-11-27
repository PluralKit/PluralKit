using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

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
        var shards = await _repo.GetShards();

        var o = new JObject();
        o.Add("shards", shards.ToJSON());
        o.Add("version", BuildInfoService.Version);

        return Ok(o);
    }
}