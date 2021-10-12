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
    public class GroupMemberControllerV2: PKControllerBase
    {
        public GroupMemberControllerV2(IServiceProvider svc) : base(svc) { }

        [HttpGet("groups/{groupRef}/members")]
        public async Task<IActionResult> GetGroupMembers(string groupRef)
        {
            var group = await ResolveGroup(groupRef);
            if (group == null)
                throw APIErrors.GroupNotFound;

            var ctx = this.ContextFor(group);

            if (!group.ListPrivacy.CanAccess(ctx))
                throw APIErrors.UnauthorizedGroupMemberList;

            var members = _repo.GetGroupMembers(group.Id).Where(m => m.MemberVisibility.CanAccess(ctx));

            var o = new JArray();

            await foreach (var member in members)
                o.Add(member.ToJson(ctx, v: APIVersion.V2));

            return Ok(o);
        }

        [HttpGet("members/{memberRef}/groups")]
        public async Task<IActionResult> GetMemberGroups(string memberRef)
        {
            var member = await ResolveMember(memberRef);
            var ctx = this.ContextFor(member);

            var system = await _repo.GetSystem(member.System);
            if (!system.GroupListPrivacy.CanAccess(ctx))
                throw APIErrors.UnauthorizedGroupList;

            var groups = _repo.GetMemberGroups(member.Id).Where(g => g.Visibility.CanAccess(ctx));

            var o = new JArray();

            await foreach (var group in groups)
                o.Add(group.ToJson(ctx));

            return Ok(o);
        }

        [HttpPut("groups/{group_id}/members/{member_id}")]
        public async Task<IActionResult> GroupMemberPut(string group_id, string member_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpPut("groups/{group_id}/members")]
        public async Task<IActionResult> GroupMembersPut(string group_id, [FromBody] JArray members)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpDelete("groups/{group_id}/members/{member_id}")]
        public async Task<IActionResult> GroupMemberDelete(string group_id, string member_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpDelete("groups/{group_id}/members")]
        public async Task<IActionResult> GroupMembersDelete(string group_id, [FromBody] JArray members)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpPut("members/{member_id}/groups")]
        public async Task<IActionResult> MemberGroupsPut(string member_id, [FromBody] JArray groups)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpDelete("members/{member_id}/groups")]
        public async Task<IActionResult> MemberGroupsDelete(string member_id, [FromBody] JArray groups)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

    }
}