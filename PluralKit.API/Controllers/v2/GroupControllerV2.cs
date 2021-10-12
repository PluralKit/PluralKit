using System;
using System.Linq;
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

        [HttpGet("systems/{systemRef}/groups")]
        public async Task<IActionResult> GetSystemGroups(string systemRef)
        {
            var system = await ResolveSystem(systemRef);
            if (system == null)
                throw APIErrors.SystemNotFound;

            var ctx = this.ContextFor(system);

            if (!system.GroupListPrivacy.CanAccess(User.ContextFor(system)))
                throw APIErrors.UnauthorizedGroupList;

            var groups = _repo.GetSystemGroups(system.Id);
            return Ok(await groups
                .Where(g => g.Visibility.CanAccess(ctx))
                .Select(g => g.ToJson(ctx))
                .ToListAsync());
        }

        [HttpPost("groups")]
        public async Task<IActionResult> GroupCreate(string group_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpGet("groups/{groupRef}")]
        public async Task<IActionResult> GroupGet(string groupRef)
        {
            var group = await ResolveGroup(groupRef);
            if (group == null)
                throw APIErrors.GroupNotFound;

            var system = await _repo.GetSystem(group.System);

            return Ok(group.ToJson(this.ContextFor(group), systemStr: system.Hid));
        }

        [HttpPatch("groups/{group_id}")]
        public async Task<IActionResult> GroupPatch(string group_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpDelete("groups/{groupRef}")]
        public async Task<IActionResult> GroupDelete(string groupRef)
        {
            var group = await ResolveGroup(groupRef);
            if (group == null)
                throw APIErrors.GroupNotFound;

            var system = await ResolveSystem("@me");
            if (system.Id != group.System)
                throw APIErrors.NotOwnGroupError;

            await _repo.DeleteGroup(group.Id);

            return NoContent();
        }
    }
}