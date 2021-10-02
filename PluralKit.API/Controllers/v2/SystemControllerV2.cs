using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("v{version:apiVersion}/systems")]
    public class SystemControllerV2: PKControllerBase
    {
        public SystemControllerV2(IServiceProvider svc) : base(svc) { }

        [HttpGet("{systemRef}")]
        public async Task<IActionResult> SystemGet(string systemRef)
        {
            var system = await ResolveSystem(systemRef);
            if (system == null) return NotFound();
            else return Ok(system.ToJson(LookupContextFor(system)));
        }

        [HttpPatch("{system}")]
        public async Task<IActionResult> SystemPatch(string system, [FromBody] JObject data)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }
    }
}