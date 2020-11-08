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
    [Route( "v{version:apiVersion}/systems" )]
    public class SystemControllerV2: PKControllerBase
    {
        [HttpGet("{id}")]
        public async Task<IActionResult> GetSystem(string id)
        {
            var system = await ResolveSystem(id);
            return Ok(system.ToApiSystem(User.ContextFor(system)));
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> UpdateSystem(string id, [FromBody] ApiSystemPatch patch)
        {
            var system = await ResolveSystem(id);
            await Authorize(system, AuthPolicies.EditSystem, $"Not allowed to edit system");

            var dbPatch = patch.ToSystemPatch();
            if (Validate(dbPatch) is ApiError e)
                return BadRequest(e);

            if (dbPatch.Equals(new SystemPatch()))
                // no-op if nothing was included in patch at all
                return Ok(system.ToApiSystem(User.ContextFor(system)));
            
            var newSystem = await Database.Execute(c => Repo.UpdateSystem(c, system.Id, dbPatch));
            return Ok(newSystem.ToApiSystem(User.ContextFor(system)));
        }

        [HttpGet("{id}/members")]
        public async Task<IActionResult> GetSystemMembers(string id)
        {
            var system = await ResolveSystem(id);
            await Authorize(system, AuthPolicies.ViewMembers, $"Not allowed to view system members");

            var ctx = User.ContextFor(system);
            await using var conn = await Database.Obtain();

            var members = await Repo.GetSystemMembers(conn, system.Id)
                .Where(m => m.MemberVisibility.CanAccess(ctx))
                .Select(m => m.ToApiMember(ctx))
                .ToListAsync();
            return Ok(members);
        }

        [HttpGet("{id}/switch")]
        public async Task<IActionResult> GetSystemFronters(string id)
        {
            var system = await ResolveSystem(id);
            await Authorize(system, AuthPolicies.ViewFront, $"Not allowed to view last system switch");

            await using var conn = await Database.Obtain();
            var ctx = User.ContextFor(system);
            var lastSwitch = await Repo.GetLatestSwitch(conn, system.Id);
            
            var memberIds = new List<Guid>();
            var membersDict = new Dictionary<Guid, ApiMember>();
            await foreach (var member in Repo.GetSwitchMembers(conn, lastSwitch.Id))
            {
                memberIds.Add(member.Uuid);
                if (!membersDict.ContainsKey(member.Uuid))
                    membersDict.Add(member.Uuid, member.ToApiMember(ctx));
            }

            return Ok(new ApiSwitchList
            {
                Switches = new[] { lastSwitch.ToApiSwitch(memberIds, ctx) },
                Members = membersDict.Values
            });
        }
        
        private ApiError? Validate(SystemPatch patch)
        {
            try
            {
                ModelValidator.ValidateSystem(patch);
            }
            catch (ModelValidationException e)
            {
                return Error(ApiErrorCode.InvalidSystemData, e.Message);
            }

            return null;
        }


        public SystemControllerV2(IServiceProvider svc): base(svc)
        {
        }
    }
}