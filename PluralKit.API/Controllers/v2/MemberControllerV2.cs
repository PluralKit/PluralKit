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
    public class MemberControllerV2: PKControllerBase
    {
        public MemberControllerV2(IServiceProvider svc) : base(svc) { }


        [HttpGet("systems/{system}/members")]
        public async Task<IActionResult> GetSystemMembers(string system)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpPost("members")]
        public async Task<IActionResult> MemberCreate([FromBody] JObject data)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpGet("members/{member}")]
        public async Task<IActionResult> MemberGet(string member)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpPatch("members/{member}")]
        public async Task<IActionResult> MemberPatch(string member, [FromBody] JObject data)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpDelete("members/{member}")]
        public async Task<IActionResult> MemberDelete(string member)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }


    }
}