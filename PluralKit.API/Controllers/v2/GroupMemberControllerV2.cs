using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

[ApiController]
[Route("v2")]
public class GroupMemberControllerV2: PKControllerBase
{
    public GroupMemberControllerV2(IServiceProvider svc) : base(svc) { }

    [HttpGet("groups/{groupRef}/members")]
    public async Task<IActionResult> GetGroupMembers(string groupRef)
    {
        var group = await ResolveGroup(groupRef);
        if (group == null)
            throw Errors.GroupNotFound;

        var ctx = ContextFor(group);

        if (!group.ListPrivacy.CanAccess(ctx))
            throw Errors.UnauthorizedGroupMemberList;

        var members = _repo.GetGroupMembers(group.Id).Where(m => m.MemberVisibility.CanAccess(ctx));

        var o = new JArray();

        await foreach (var member in members)
            o.Add(member.ToJson(ctx));

        return Ok(o);
    }

    [HttpPost("groups/{groupRef}/members/add")]
    public async Task<IActionResult> AddGroupMembers(string groupRef, [FromBody] JArray memberRefs)
    {
        if (memberRefs.Count == 0)
            throw Errors.GenericBadRequest;

        var system = await ResolveSystem("@me");

        var group = await ResolveGroup(groupRef);
        if (group == null)
            throw Errors.GroupNotFound;
        if (group.System != system.Id)
            throw Errors.NotOwnGroupError;

        var members = new List<MemberId>();

        foreach (var JmemberRef in memberRefs)
        {
            var memberRef = JmemberRef.Value<string>();
            var member = await ResolveMember(memberRef, cache: true);

            // todo: have a list of these errors instead of immediately throwing

            if (member == null)
                throw Errors.MemberNotFoundWithRef(memberRef);
            if (member.System != system.Id)
                throw Errors.NotOwnMemberErrorWithRef(memberRef);

            members.Add(member.Id);
        }

        var existingMembers = await _repo.GetGroupMembers(group.Id).Select(x => x.Id).ToListAsync();
        members = members.Where(x => !existingMembers.Contains(x)).ToList();

        if (members.Count > 0)
            await _repo.AddMembersToGroup(group.Id, members);

        return NoContent();
    }

    [HttpPost("groups/{groupRef}/members/remove")]
    public async Task<IActionResult> RemoveGroupMembers(string groupRef, [FromBody] JArray memberRefs)
    {
        if (memberRefs.Count == 0)
            throw Errors.GenericBadRequest;

        var system = await ResolveSystem("@me");

        var group = await ResolveGroup(groupRef);
        if (group == null)
            throw Errors.GroupNotFound;
        if (group.System != system.Id)
            throw Errors.NotOwnGroupError;

        var members = new List<MemberId>();

        foreach (var JmemberRef in memberRefs)
        {
            var memberRef = JmemberRef.Value<string>();
            var member = await ResolveMember(memberRef, cache: true);

            if (member == null)
                throw Errors.MemberNotFoundWithRef(memberRef);
            if (member.System != system.Id)
                throw Errors.NotOwnMemberErrorWithRef(memberRef);

            members.Add(member.Id);
        }

        await _repo.RemoveMembersFromGroup(group.Id, members);

        return NoContent();
    }

    [HttpPost("groups/{groupRef}/members/overwrite")]
    public async Task<IActionResult> OverwriteGroupMembers(string groupRef, [FromBody] JArray memberRefs)
    {
        var system = await ResolveSystem("@me");

        var group = await ResolveGroup(groupRef);
        if (group == null)
            throw Errors.GroupNotFound;
        if (group.System != system.Id)
            throw Errors.NotOwnGroupError;

        var members = new List<MemberId>();

        foreach (var JmemberRef in memberRefs)
        {
            var memberRef = JmemberRef.Value<string>();
            var member = await ResolveMember(memberRef, cache: true);

            if (member == null)
                throw Errors.MemberNotFoundWithRef(memberRef);
            if (member.System != system.Id)
                throw Errors.NotOwnMemberErrorWithRef(memberRef);

            members.Add(member.Id);
        }

        await _repo.ClearGroupMembers(group.Id);

        if (members.Count > 0)
            await _repo.AddMembersToGroup(group.Id, members);

        return NoContent();
    }


    [HttpGet("members/{memberRef}/groups")]
    public async Task<IActionResult> GetMemberGroups(string memberRef)
    {
        var member = await ResolveMember(memberRef);
        var ctx = ContextFor(member);

        var system = await _repo.GetSystem(member.System);
        if (!system.GroupListPrivacy.CanAccess(ctx))
            throw Errors.UnauthorizedGroupList;

        var groups = _repo.GetMemberGroups(member.Id).Where(g => g.Visibility.CanAccess(ctx));

        var o = new JArray();

        await foreach (var group in groups)
            o.Add(group.ToJson(ctx));

        return Ok(o);
    }

    [HttpPost("members/{memberRef}/groups/add")]
    public async Task<IActionResult> AddMemberGroups(string memberRef, [FromBody] JArray groupRefs)
    {
        if (groupRefs.Count == 0)
            throw Errors.GenericBadRequest;

        var system = await ResolveSystem("@me");

        var member = await ResolveMember(memberRef);
        if (member == null)
            throw Errors.MemberNotFound;
        if (member.System != system.Id)
            throw Errors.NotOwnMemberError;

        var groups = new List<GroupId>();

        foreach (var JgroupRef in groupRefs)
        {
            var groupRef = JgroupRef.Value<string>();
            var group = await ResolveGroup(groupRef, cache: true);

            if (group == null)
                throw Errors.GroupNotFound;
            if (group.System != system.Id)
                throw Errors.NotOwnGroupErrorWithRef(groupRef);

            groups.Add(group.Id);
        }

        var existingGroups = await _repo.GetMemberGroups(member.Id).Select(x => x.Id).ToListAsync();
        groups = groups.Where(x => !existingGroups.Contains(x)).ToList();

        if (groups.Count > 0)
            await _repo.AddGroupsToMember(member.Id, groups);

        return NoContent();
    }

    [HttpPost("members/{memberRef}/groups/remove")]
    public async Task<IActionResult> RemoveMemberGroups(string memberRef, [FromBody] JArray groupRefs)
    {
        if (groupRefs.Count == 0)
            throw Errors.GenericBadRequest;

        var system = await ResolveSystem("@me");

        var member = await ResolveMember(memberRef);
        if (member == null)
            throw Errors.MemberNotFound;
        if (member.System != system.Id)
            throw Errors.NotOwnMemberError;

        var groups = new List<GroupId>();

        foreach (var JgroupRef in groupRefs)
        {
            var groupRef = JgroupRef.Value<string>();
            var group = await ResolveGroup(groupRef, cache: true);

            if (group == null)
                throw Errors.GroupNotFoundWithRef(groupRef);
            if (group.System != system.Id)
                throw Errors.NotOwnGroupErrorWithRef(groupRef);

            groups.Add(group.Id);
        }

        await _repo.RemoveGroupsFromMember(member.Id, groups);

        return NoContent();
    }

    [HttpPost("members/{memberRef}/groups/overwrite")]
    public async Task<IActionResult> OverwriteMemberGroups(string memberRef, [FromBody] JArray groupRefs)
    {
        var system = await ResolveSystem("@me");

        var member = await ResolveMember(memberRef);
        if (member == null)
            throw Errors.MemberNotFound;
        if (member.System != system.Id)
            throw Errors.NotOwnMemberError;

        var groups = new List<GroupId>();

        foreach (var JgroupRef in groupRefs)
        {
            var groupRef = JgroupRef.Value<string>();
            var group = await ResolveGroup(groupRef, cache: true);

            if (group == null)
                throw Errors.GroupNotFoundWithRef(groupRef);
            if (group.System != system.Id)
                throw Errors.NotOwnGroupErrorWithRef(groupRef);

            groups.Add(group.Id);
        }

        await _repo.ClearMemberGroups(member.Id);

        if (groups.Count > 0)
            await _repo.AddGroupsToMember(member.Id, groups);

        return NoContent();
    }
}