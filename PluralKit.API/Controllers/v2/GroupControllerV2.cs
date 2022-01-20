using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

[ApiController]
[Route("v2")]
public class GroupControllerV2: PKControllerBase
{
    public GroupControllerV2(IServiceProvider svc) : base(svc) { }

    [HttpGet("systems/{systemRef}/groups")]
    public async Task<IActionResult> GetSystemGroups(string systemRef, [FromQuery] bool with_members)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null)
            throw Errors.SystemNotFound;

        var ctx = ContextFor(system);

        if (with_members && !system.MemberListPrivacy.CanAccess(ctx))
            throw Errors.UnauthorizedMemberList;

        if (!system.GroupListPrivacy.CanAccess(User.ContextFor(system)))
            throw Errors.UnauthorizedGroupList;

        var groups = _repo.GetSystemGroups(system.Id);

        var j_groups = await groups
            .Where(g => g.Visibility.CanAccess(ctx))
            .Select(g => g.ToJson(ctx, needsMembersArray: with_members))
            .ToListAsync();

        if (with_members && !system.MemberListPrivacy.CanAccess(ctx))
            throw Errors.UnauthorizedMemberList;

        if (with_members && j_groups.Count > 0)
        {
            var q = await _repo.GetGroupMemberInfo(await groups.Select(x => x.Id).ToListAsync());

            foreach (var row in q)
                if (row.MemberVisibility.CanAccess(ctx))
                    ((JArray)j_groups.Find(x => x.Value<string>("id") == row.Group)["members"]).Add(row.MemberUuid);
        }

        return Ok(j_groups);
    }

    [HttpPost("groups")]
    public async Task<IActionResult> GroupCreate([FromBody] JObject data)
    {
        var system = await ResolveSystem("@me");
        var config = await _repo.GetSystemConfig(system.Id);

        // Check group cap
        var existingGroupCount = await _repo.GetSystemGroupCount(system.Id);
        var groupLimit = config.GroupLimitOverride ?? Limits.MaxGroupCount;
        if (existingGroupCount >= groupLimit)
            throw Errors.GroupLimitReached;

        var patch = GroupPatch.FromJson(data);
        patch.AssertIsValid();
        if (!patch.Name.IsPresent)
            patch.Errors.Add(new ValidationError("name", "Key 'name' is required when creating new group."));
        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        using var conn = await _db.Obtain();
        using var tx = await conn.BeginTransactionAsync();

        var newGroup = await _repo.CreateGroup(system.Id, patch.Name.Value, conn);
        newGroup = await _repo.UpdateGroup(newGroup.Id, patch, conn);

        _ = _dispatch.Dispatch(newGroup.Id, new UpdateDispatchData()
        {
            Event = DispatchEvent.CREATE_GROUP,
            EventData = patch.ToJson(),
        });

        await tx.CommitAsync();

        return Ok(newGroup.ToJson(LookupContext.ByOwner));
    }

    [HttpGet("groups/{groupRef}")]
    public async Task<IActionResult> GroupGet(string groupRef)
    {
        var group = await ResolveGroup(groupRef);
        if (group == null)
            throw Errors.GroupNotFound;

        var system = await _repo.GetSystem(group.System);

        return Ok(group.ToJson(ContextFor(group), system.Hid));
    }

    [HttpPatch("groups/{groupRef}")]
    public async Task<IActionResult> DoGroupPatch(string groupRef, [FromBody] JObject data)
    {
        var system = await ResolveSystem("@me");
        var group = await ResolveGroup(groupRef);
        if (group == null)
            throw Errors.GroupNotFound;
        if (group.System != system.Id)
            throw Errors.NotOwnGroupError;

        var patch = GroupPatch.FromJson(data);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        var newGroup = await _repo.UpdateGroup(group.Id, patch);
        return Ok(newGroup.ToJson(LookupContext.ByOwner));
    }

    [HttpDelete("groups/{groupRef}")]
    public async Task<IActionResult> GroupDelete(string groupRef)
    {
        var group = await ResolveGroup(groupRef);
        if (group == null)
            throw Errors.GroupNotFound;

        var system = await ResolveSystem("@me");
        if (system.Id != group.System)
            throw Errors.NotOwnGroupError;

        await _repo.DeleteGroup(group.Id);

        return NoContent();
    }
}