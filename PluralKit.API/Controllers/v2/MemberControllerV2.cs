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
    [Route("v{version:apiVersion}/members")]
    public class MemberControllerV2: PKControllerBase
    {
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMember(string id)
        {
            var member = await ResolveMember(id);
            return Ok(member.ToApiMember(User.ContextFor(member)));
        }

        [HttpPost("")]
        public async Task<IActionResult> CreateMember([FromBody] ApiMemberPatch patch)
        {
            if (!User.TryGetCurrentSystem(out var currentSystem))
                return Unauthorized(Error(ApiErrorCode.NotAuthenticated,
                    "Cannot create a new member without token authentication"));
            
            if (!patch.Name.IsPresent || string.IsNullOrWhiteSpace(patch.Name.Value))
                return BadRequest(Error(ApiErrorCode.MemberNameRequired,
                    "Member name is required when creating a new member"));

            var dbPatch = patch.ToMemberPatch();
            if (Validate(dbPatch) is ApiError e)
                throw new ApiErrorException(HttpStatusCode.BadRequest, e);
            
            await using var conn = await Database.Obtain();

            var system = await Repo.GetSystem(conn, currentSystem);
            var memberCount = await Repo.GetSystemMemberCount(conn, currentSystem);
            var memberLimit = system?.MemberLimitOverride ?? Limits.MaxMemberCount;
            if (memberCount >= memberLimit)
                return BadRequest(Error(ApiErrorCode.MemberLimitReached,
                    $"Member limit reached for system ({memberLimit} members)"));
            
            var member = await Repo.CreateMember(conn, currentSystem, patch.Name.Value);
            member = await Repo.UpdateMember(conn, member.Id, patch.ToMemberPatch());
            return Ok(member.ToApiMember(User.ContextFor(member)));
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> PatchMember(string id, [FromBody] ApiMemberPatch patch)
        {
            var member = await ResolveMember(id);
            await Authorize(member, AuthPolicies.EditMember, $"Not allowed to edit member of another system");

            var dbPatch = patch.ToMemberPatch();
            if (Validate(dbPatch) is ApiError e)
                throw new ApiErrorException(HttpStatusCode.BadRequest, e);
            
            if (dbPatch.Equals(new MemberPatch()))
                // no-op if nothing was included in patch at all
                return Ok(member.ToApiMember(User.ContextFor(member)));

            var newMember = await Database.Execute(c => Repo.UpdateMember(c, member.Id, dbPatch));
            return Ok(newMember.ToApiMember(User.ContextFor(newMember)));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMember(string id)
        {
            var member = await ResolveMember(id);
            await Authorize(member, AuthPolicies.DeleteMember, $"Not allowed to delete member");

            await Database.Execute(c => Repo.DeleteMember(c, member.Id));
            return Ok(member.ToApiMember(User.ContextFor(member)));
        }

        private ApiError? Validate(MemberPatch patch)
        {
            try
            {
                ModelValidator.ValidateMember(patch);
            }
            catch (ModelValidationException e)
            {
                return Error(ApiErrorCode.InvalidMemberData, e.Message);
            }

            return null;
        }

        public MemberControllerV2(IServiceProvider svc): base(svc) { }
    }
}