using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

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

        [HttpPut("groups/{groupRef}/members/{memberRef}")]
        public async Task<IActionResult> GroupMemberPut(string groupRef, string memberRef)
        {
            var system = await ResolveSystem("@me");

            var group = await ResolveGroup(groupRef);
            if (group == null)
                throw APIErrors.GroupNotFound;
            if (group.System != system.Id)
                throw APIErrors.NotOwnGroupError;

            var member = await ResolveMember(memberRef);
            Console.WriteLine(member);
            if (member == null)
                throw APIErrors.MemberNotFound;
            if (member.System != system.Id)
                throw APIErrors.NotOwnMemberError;

            var existingMembers = await _repo.GetGroupMembers(group.Id).Select(x => x.Id).ToListAsync();
            if (!existingMembers.Contains(member.Id))
                await _repo.AddMembersToGroup(group.Id, new List<MemberId>() { member.Id });

            return NoContent();
        }

        [HttpPut("groups/{groupRef}/members")]
        public async Task<IActionResult> GroupMembersPut(string groupRef, [FromBody] JArray memberRefs)
        {
            if (memberRefs.Count == 0)
                throw APIErrors.GenericBadRequest;

            var system = await ResolveSystem("@me");

            var group = await ResolveGroup(groupRef);
            if (group == null)
                throw APIErrors.GroupNotFound;
            if (group.System != system.Id)
                throw APIErrors.NotOwnGroupError;

            var members = new List<MemberId>();

            foreach (var JmemberRef in memberRefs)
            {
                var memberRef = JmemberRef.Value<string>();
                var member = await ResolveMember(memberRef);

                if (member == null)
                    throw APIErrors.MemberNotFound;
                if (member.System != system.Id)
                    throw APIErrors.NotOwnMemberErrorWithRef(memberRef);

                members.Add(member.Id);
            }

            var existingMembers = await _repo.GetGroupMembers(group.Id).Select(x => x.Id).ToListAsync();
            members = members.Where(x => !existingMembers.Contains(x)).ToList();

            if (members.Count > 0)
                await _repo.AddMembersToGroup(group.Id, members);

            return NoContent();
        }

        [HttpDelete("groups/{groupRef}/members/{memberRef}")]
        public async Task<IActionResult> GroupMemberDelete(string groupRef, string memberRef)
        {
            var system = await ResolveSystem("@me");

            var group = await ResolveGroup(groupRef);
            if (group == null)
                throw APIErrors.GroupNotFound;
            if (group.System != system.Id)
                throw APIErrors.NotOwnGroupError;

            var member = await ResolveMember(memberRef);
            if (member == null)
                throw APIErrors.MemberNotFound;
            if (member.System != system.Id)
                throw APIErrors.NotOwnMemberError;

            await _repo.RemoveMembersFromGroup(group.Id, new List<MemberId>() { member.Id });

            return NoContent();
        }

        [HttpDelete("groups/{groupRef}/members")]
        public async Task<IActionResult> GroupMembersDelete(string groupRef, [FromBody] JArray memberRefs)
        {
            if (memberRefs.Count == 0)
                throw APIErrors.GenericBadRequest;

            var system = await ResolveSystem("@me");

            var group = await ResolveGroup(groupRef);
            if (group == null)
                throw APIErrors.GroupNotFound;
            if (group.System != system.Id)
                throw APIErrors.NotOwnGroupError;

            var members = new List<MemberId>();

            foreach (var JmemberRef in memberRefs)
            {
                var memberRef = JmemberRef.Value<string>();
                var member = await ResolveMember(memberRef);

                if (member == null)
                    throw APIErrors.MemberNotFound;
                if (member.System != system.Id)
                    throw APIErrors.NotOwnMemberError;

                members.Add(member.Id);
            }

            await _repo.RemoveMembersFromGroup(group.Id, members);

            return NoContent();
        }

        [HttpPut("members/{memberRef}/groups")]
        public async Task<IActionResult> MemberGroupsPut(string memberRef, [FromBody] JArray groupRefs)
        {
            if (groupRefs.Count == 0)
                throw APIErrors.GenericBadRequest;

            var system = await ResolveSystem("@me");

            var member = await ResolveMember(memberRef);
            if (member == null)
                throw APIErrors.MemberNotFound;
            if (member.System != system.Id)
                throw APIErrors.NotOwnMemberError;

            var groups = new List<GroupId>();

            foreach (var JgroupRef in groupRefs)
            {
                var groupRef = JgroupRef.Value<string>();
                var group = await ResolveGroup(groupRef);

                if (group == null)
                    throw APIErrors.GroupNotFound;
                if (group.System != system.Id)
                    throw APIErrors.NotOwnGroupErrorWithRef(groupRef);

                groups.Add(group.Id);
            }

            var existingGroups = await _repo.GetMemberGroups(member.Id).Select(x => x.Id).ToListAsync();
            groups = groups.Where(x => !existingGroups.Contains(x)).ToList();

            if (groups.Count > 0)
                await _repo.AddGroupsToMember(member.Id, groups);

            return NoContent();
        }

        [HttpDelete("members/{memberRef}/groups")]
        public async Task<IActionResult> MemberGroupsDelete(string memberRef, [FromBody] JArray groupRefs)
        {
            if (groupRefs.Count == 0)
                throw APIErrors.GenericBadRequest;

            var system = await ResolveSystem("@me");

            var member = await ResolveMember(memberRef);
            if (member == null)
                throw APIErrors.MemberNotFound;
            if (member.System != system.Id)
                throw APIErrors.NotOwnMemberError;

            var groups = new List<GroupId>();

            foreach (var JgroupRef in groupRefs)
            {
                var groupRef = JgroupRef.Value<string>();
                var group = await ResolveGroup(groupRef);

                if (group == null)
                    throw APIErrors.GroupNotFound;
                if (group.System != system.Id)
                    throw APIErrors.NotOwnGroupErrorWithRef(groupRef);

                groups.Add(group.Id);
            }

            await _repo.RemoveGroupsFromMember(member.Id, groups);

            return NoContent();
        }

    }
}