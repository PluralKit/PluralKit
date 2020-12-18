using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using PluralKit.API.Models;

namespace PluralKit.API.v2
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("v{version:apiVersion}/switches")]
    public class SwitchControllerV2: PKControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> CreateSwitch([FromBody] ApiSwitchPatch patch)
        {
            if (!User.TryGetCurrentSystem(out var currentSystem))
                return Unauthorized(Error(ApiErrorCode.NotAuthenticated,
                    "Cannot create a new switch without token authentication"));
            
            if (!patch.Members.IsPresent)
                return BadRequest(Error(ApiErrorCode.SwitchMembersRequired,
                    "Switch member list is required when creating a new switch"));
            
            // TODO: resolve switch members without doing one lookup every time...
            return NotFound("TODO");
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteSwitch(Guid id)
        {
            await using var conn = await Database.Obtain();

            var sw = await Repo.GetSwitchByUuid(conn, id);
            await Authorize(sw, AuthPolicies.DeleteSwitch, $"Not allowed to delete switch");

            await Repo.DeleteSwitch(conn, sw.Id);
            return NoContent();
        }
        
        public SwitchControllerV2(IServiceProvider svc): base(svc) { }
    }
}