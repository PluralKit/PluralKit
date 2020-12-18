#nullable enable
using System;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using PluralKit.API.Models;
using PluralKit.Core;
using PluralKit.Core.Validation;

namespace PluralKit.API.v2
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("v{version:apiVersion}/groups")]
    public class GroupControllerV2: PKControllerBase
    {
        public GroupControllerV2(IServiceProvider svc): base(svc)
        {

        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGroup(string id)
        {
            var group = await ResolveGroup(id);
            return Ok(group.ToApiGroup(User.ContextFor(group)));
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromBody] ApiGroupPatch patch)
        {
            if (!User.TryGetCurrentSystem(out var currentSystem))
                return Unauthorized(Error(ApiErrorCode.NotAuthenticated,
                    "Cannot create a new group without token authentication"));
            
            if (!patch.Name.IsPresent || string.IsNullOrWhiteSpace(patch.Name.Value))
                return BadRequest(Error(ApiErrorCode.GroupNameRequired,
                    "Group name is required when creating a new group"));

            var dbPatch = patch.ToGroupPatch();
            if (Validate(dbPatch) is ApiError e)
                throw new ApiErrorException(HttpStatusCode.BadRequest, e);
            
            await using var conn = await Database.Obtain();

            var system = await Repo.GetSystem(conn, currentSystem);
            var groupCount = await Repo.GetSystemGroupCount(conn, currentSystem);
            var groupLimit = system?.GroupLimitOverride ?? Limits.MaxGroupCount;
            if (groupCount >= groupLimit)
                return BadRequest(Error(ApiErrorCode.GroupLimitReached,
                    $"Group limit reached for system ({groupLimit} groups)"));
            
            var group = await Repo.CreateGroup(conn, currentSystem, patch.Name.Value);
            group = await Repo.UpdateGroup(conn, group.Id, patch.ToGroupPatch());
            return Ok(group.ToApiGroup(User.ContextFor(group)));
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchGroup(string id, [FromBody] ApiGroupPatch patch)
        {
            var group = await ResolveGroup(id);
            await Authorize(group, AuthPolicies.EditGroup, $"Not allowed to edit group of another system");

            var dbPatch = patch.ToGroupPatch();
            if (Validate(dbPatch) is ApiError e)
                throw new ApiErrorException(HttpStatusCode.BadRequest, e);
            
            if (dbPatch.Equals(new GroupPatch()))
                // no-op if nothing was included in patch at all
                return Ok(group.ToApiGroup(User.ContextFor(group)));

            var newGroup = await Database.Execute(c => Repo.UpdateGroup(c, group.Id, dbPatch));
            return Ok(newGroup.ToApiGroup(User.ContextFor(newGroup)));
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteGroup(string id)
        {
            var group = await ResolveGroup(id);
            await Authorize(group, AuthPolicies.DeleteGroup, $"Not allowed to delete group");

            await Database.Execute(c => Repo.DeleteGroup(c, group.Id));
            return Ok(group.ToApiGroup(User.ContextFor(group)));
        }

        [HttpGet("{id}/members")]
        public async Task<IActionResult> GetGroupMembers(string id)
        {
            // TODO: auth view permission, get list of members, return member objects
            return NotFound("TODO");
        }

        [HttpPatch("{id}/members")]
        public async Task<IActionResult> UpdateGroupMembers(string id, [FromBody] ApiGroupMembersPatch patch)
        {
            // var group = await ResolveGroup(id);
            // await Authorize(group, AuthPolicies.EditGroup, $"Not allowed to edit group members");

            // TODO: auth view permission? check whether members belong to the group, do adds/removes, return remaining
            // Ideally we'd do this without N or more individual member ID lookups...
            return NotFound("TODO");
        }
        
        private ApiError? Validate(GroupPatch patch)
        {
            try
            {
                ModelValidator.ValidateGroup(patch);
            }
            catch (ModelValidationException e)
            {
                return Error(ApiErrorCode.InvalidGroupData, e.Message);
            }

            return null;
        }
    }
}