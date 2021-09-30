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
    public class GuildControllerV2: PKControllerBase
    {
        public GuildControllerV2(IServiceProvider svc) : base(svc) { }


        [HttpGet("systems/{system}/guilds/{guild_id}")]
        public async Task<IActionResult> SystemGuildGet(string system, ulong guild_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpPatch("systems/{system}/guilds/{guild_id}")]
        public async Task<IActionResult> SystemGuildPatch(string system, ulong guild_id, [FromBody] JObject data)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpGet("members/{member}/guilds/{guild_id}")]
        public async Task<IActionResult> MemberGuildGet(string member, ulong guild_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpPatch("members/{member}/guilds/{guild_id}")]
        public async Task<IActionResult> MemberGuildPatch(string member, ulong guild_id, [FromBody] JObject data)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }


    }
}