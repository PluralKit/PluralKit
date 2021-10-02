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
    public class GroupControllerV2: PKControllerBase
    {
        public GroupControllerV2(IServiceProvider svc) : base(svc) { }

        [HttpGet("systems/{system_id}/groups")]
        public async Task<IActionResult> GetSystemGroups(string system_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpPost("groups")]
        public async Task<IActionResult> GroupCreate(string group_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpGet("groups/{group_id}")]
        public async Task<IActionResult> GroupGet(string group_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpPatch("groups/{group_id}")]
        public async Task<IActionResult> GroupPatch(string group_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpDelete("groups/{group_id}")]
        public async Task<IActionResult> GroupDelete(string group_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }


    }
}