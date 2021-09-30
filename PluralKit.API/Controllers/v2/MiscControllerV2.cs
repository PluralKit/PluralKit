using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("v{version:apiVersion}")]
    public class MetaControllerV2: PKControllerBase
    {
        public MetaControllerV2(IServiceProvider svc) : base(svc) { }

        [HttpGet("meta")]
        public async Task<ActionResult<JObject>> Meta()
        {
            await using var conn = await _db.Obtain();
            var shards = await _repo.GetShards(conn);

            var o = new JObject();
            o.Add("shards", shards.ToJSON());

            return Ok(o);
        }

        [HttpGet("messages/{message_id}")]
        public async Task<IActionResult> MessageGet(ulong message_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }
    }
}