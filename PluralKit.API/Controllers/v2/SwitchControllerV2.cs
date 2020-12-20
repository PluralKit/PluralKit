#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using PluralKit.API.Models;
using PluralKit.Core;
using PluralKit.Core.Validation;

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
            
            // TODO: move/remove limit
            if (patch.Members.Value.Count > 20)
                return BadRequest(Error(ApiErrorCode.TooManySwitchMembers, "Too many switch members (max. 20)"));

            var dbPatch = patch.ToSwitchPatch();
            if (Validate(dbPatch) is ApiError e)
                return BadRequest(e);

            await using var conn = await Database.Obtain();
            
            var members = new List<PKMember>();
            foreach (var id in patch.Members.Value)
            {
                var member = await Repo.GetMemberByGuid(conn, id);
                
                if (member == null)
                    return BadRequest(Error(ApiErrorCode.MemberNotFound, $"Could not find member by UUID '{id}'"));
                
                if (member.System.Value != currentSystem.Value)
                    return BadRequest(Error(ApiErrorCode.SwitchMemberNotInSystem,
                $"Cannot create switch with member '{id}' in a different system"));

                if (members.Any(m => m.Uuid == id))
                    return BadRequest(Error(ApiErrorCode.DuplicateSwitchMember, $"Duplicate switch member '{id}'"));
                
                members.Add(member);
            }

            var newSwitch = await Repo.AddSwitch(conn, new SystemId(currentSystem.Value), members.Select(m => m.Id).ToList());
            if (dbPatch != new SwitchPatch())
                newSwitch = await Repo.UpdateSwitch(conn, newSwitch.Id, dbPatch);
            
            return Ok(newSwitch.ToApiSwitch(members.Select(m => m.Uuid), User.ContextFor(newSwitch)));
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateSwitch(Guid id, [FromBody] ApiSwitchPatch patch)
        {
            if (patch.Members.IsPresent)
                return BadRequest(Error(ApiErrorCode.CannotUpdateSwitchMembers, "Cannot update existing switch members"));
            
            await using var conn = await Database.Obtain();

            var sw = await Repo.GetSwitchByUuid(conn, id);
            await Authorize(sw, AuthPolicies.EditSwitch, $"Not allowed to update switch");
            
            var dbPatch = patch.ToSwitchPatch();
            if (Validate(dbPatch) is ApiError e)
                return BadRequest(e);

            sw = await Repo.UpdateSwitch(conn, sw.Id, dbPatch);
            
            var members = Repo.GetSwitchMembers(conn, sw.Id);
            var memberGuids = await members.Select(m => m.Uuid).ToListAsync();

            return Ok(sw.ToApiSwitch(memberGuids, User.ContextFor(sw)));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSwitch(Guid id)
        {
            await using var conn = await Database.Obtain();
            var sw = await Repo.GetSwitchByUuid(conn, id);
            if (sw == null)
                return NotFound(Error(ApiErrorCode.SwitchNotFound, "Switch with ID '{id}' not found"));

            var members = Repo.GetSwitchMembers(conn, sw.Id);
            var memberGuids = await members.Select(m => m.Uuid).ToListAsync();
            
            return Ok(sw.ToApiSwitch(memberGuids, User.ContextFor(sw)));
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
        
        private ApiError? Validate(SwitchPatch patch)
        {
            try
            {
                ModelValidator.ValidateSwitch(patch);
            }
            catch (ModelValidationException e)
            {
                return Error(ApiErrorCode.InvalidSwitchData, e.Message);
            }

            return null;
        }
        
        public SwitchControllerV2(IServiceProvider svc): base(svc) { }
    }
}