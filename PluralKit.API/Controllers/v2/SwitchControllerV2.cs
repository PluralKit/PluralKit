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
    public class SwitchControllerV2: PKControllerBase
    {
        public SwitchControllerV2(IServiceProvider svc) : base(svc) { }


        [HttpGet("systems/{system}/switches")]
        public async Task<IActionResult> GetSystemSwitches(string system)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpGet("systems/{system}/fronters")]
        public async Task<IActionResult> GetSystemFronters(string system)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }


        [HttpPost("systems/{system}/switches")]
        public async Task<IActionResult> SwitchCreate(string system, [FromBody] JObject data)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }


        [HttpGet("systems/{system}/switches/{switch_id}")]
        public async Task<IActionResult> SwitchGet(string system, string switch_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpPatch("systems/{system}/switches/{switch_id}")]
        public async Task<IActionResult> SwitchPatch(string system, [FromBody] JObject data)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpDelete("systems/{system}/switches/{switch_id}")]
        public async Task<IActionResult> SwitchDelete(string system, string switch_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

    }
}