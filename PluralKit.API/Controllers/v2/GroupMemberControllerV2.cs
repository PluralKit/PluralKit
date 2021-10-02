using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("v{version:apiVersion}")]
    public class GroupMemberControllerV2: PKControllerBase
    {
        public GroupMemberControllerV2(IServiceProvider svc) : base(svc) { }

        [HttpGet("groups/{group_id}/members")]
        public async Task<IActionResult> GetGroupMembers(string group_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpGet("members/{member_id}/groups")]
        public async Task<IActionResult> GetMemberGroups(string member_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
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