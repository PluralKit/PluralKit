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
    public class MemberControllerV2: PKControllerBase
    {
        public MemberControllerV2(IServiceProvider svc) : base(svc) { }


        [HttpGet("systems/{systemRef}/members")]
        public async Task<IActionResult> GetSystemMembers(string systemRef)
        {
            var system = await ResolveSystem(systemRef);
            if (system == null)
                throw Errors.SystemNotFound;

            var ctx = this.ContextFor(system);

            if (!system.MemberListPrivacy.CanAccess(this.ContextFor(system)))
                throw Errors.UnauthorizedMemberList;

            var members = _repo.GetSystemMembers(system.Id);
            return Ok(await members
                .Where(m => m.MemberVisibility.CanAccess(ctx))
                .Select(m => m.ToJson(ctx, v: APIVersion.V2))
                .ToListAsync());
        }

        [HttpPost("members")]
        public async Task<IActionResult> MemberCreate([FromBody] JObject data)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpGet("members/{memberRef}")]
        public async Task<IActionResult> MemberGet(string memberRef)
        {
            var member = await ResolveMember(memberRef);
            if (member == null)
                throw Errors.MemberNotFound;

            var system = await _repo.GetSystem(member.System);

            return Ok(member.ToJson(this.ContextFor(member), systemStr: system.Hid, v: APIVersion.V2));
        }

        [HttpPatch("members/{member}")]
        public async Task<IActionResult> MemberPatch(string member, [FromBody] JObject data)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpDelete("members/{memberRef}")]
        public async Task<IActionResult> MemberDelete(string memberRef)
        {
            var member = await ResolveMember(memberRef);
            if (member == null)
                throw Errors.MemberNotFound;

            var system = await ResolveSystem("@me");
            if (system.Id != member.System)
                throw Errors.NotOwnMemberError;

            await _repo.DeleteMember(member.Id);

            return NoContent();
        }
    }
}