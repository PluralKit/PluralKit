using Dapper;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API;

[ApiController]
[Route("v2")]
public class SwitchControllerV2: PKControllerBase
{
    public SwitchControllerV2(IServiceProvider svc) : base(svc) { }


    [HttpGet("systems/{systemRef}/switches")]
    public async Task<IActionResult> GetSystemSwitches(string systemRef,
                                                       [FromQuery(Name = "before")] Instant? before,
                                                       [FromQuery(Name = "limit")] int? limit)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null)
            throw Errors.SystemNotFound;

        var ctx = ContextFor(system);

        if (!system.FrontHistoryPrivacy.CanAccess(ctx))
            throw Errors.UnauthorizedFrontHistory;

        if (before == null)
            before = SystemClock.Instance.GetCurrentInstant();

        if (limit == null || limit > 100)
            limit = 100;

        var res = await _db.Execute(conn => conn.QueryAsync<SwitchesReturnNew>(
            @"select *, array(
                    select members.hid from switch_members, members
                    where switch_members.switch = switches.id and members.id = switch_members.member
                ) as members from switches
                where switches.system = @System and switches.timestamp < @Before
                order by switches.timestamp desc
                limit @Limit;",
            new { System = system.Id, Before = before, Limit = limit }
        ));
        return Ok(res);
    }

    [HttpGet("systems/{systemRef}/fronters")]
    public async Task<IActionResult> GetSystemFronters(string systemRef)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null)
            throw Errors.SystemNotFound;

        var ctx = ContextFor(system);

        if (!system.FrontPrivacy.CanAccess(ctx))
            throw Errors.UnauthorizedCurrentFronters;

        var sw = await _repo.GetLatestSwitch(system.Id);
        if (sw == null)
            return NoContent();

        var members = _db.Execute(conn => _repo.GetSwitchMembers(conn, sw.Id));
        return Ok(new FrontersReturnNew
        {
            Timestamp = sw.Timestamp,
            Members = await members.Select(m => m.ToJson(ctx, v: APIVersion.V2)).ToListAsync(),
            Uuid = sw.Uuid,
        });
    }


    [HttpPost("systems/{systemRef}/switches")]
    public async Task<IActionResult> SwitchCreate(string systemRef, [FromBody] PostSwitchParams data)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null) throw Errors.SystemNotFound;
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        if (data.Members.Distinct().Count() != data.Members.Count)
            throw Errors.DuplicateMembersInList;

        if (data.Timestamp != null && await _repo.GetSwitches(system.Id).Select(x => x.Timestamp)
                .ContainsAsync(data.Timestamp.Value))
            throw Errors.SameSwitchTimestampError;

        var members = new List<PKMember>();

        foreach (var memberRef in data.Members)
        {
            var member = await ResolveMember(memberRef);
            if (member == null)
                throw Errors.MemberNotFoundWithRef(memberRef);
            if (member.System != system.Id)
                throw Errors.NotOwnMemberErrorWithRef(memberRef);
            members.Add(member);
        }

        // We get the current switch, if it exists
        var latestSwitch = await _repo.GetLatestSwitch(system.Id);
        if (latestSwitch != null && (data.Timestamp == null || data.Timestamp > latestSwitch.Timestamp))
        {
            var latestSwitchMembers = _db.Execute(conn => _repo.GetSwitchMembers(conn, latestSwitch.Id));

            // Bail if this switch is identical to the latest one
            if (await latestSwitchMembers.Select(m => m.Hid)
                    .SequenceEqualAsync(members.Select(m => m.Hid).ToAsyncEnumerable()))
                throw Errors.SameSwitchMembersError;
        }

        var newSwitch =
            await _db.Execute(conn => _repo.AddSwitch(conn, system.Id, members.Select(m => m.Id).ToList()));
        if (data.Timestamp != null)
            await _repo.MoveSwitch(newSwitch.Id, data.Timestamp.Value);

        return Ok(new FrontersReturnNew
        {
            Uuid = newSwitch.Uuid,
            Timestamp = data.Timestamp != null ? data.Timestamp.Value : newSwitch.Timestamp,
            Members = members.Select(x => x.ToJson(LookupContext.ByOwner, v: APIVersion.V2)),
        });
    }


    [HttpGet("systems/{systemRef}/switches/{switchRef}")]
    public async Task<IActionResult> SwitchGet(string systemRef, string switchRef)
    {
        if (!Guid.TryParse(switchRef, out var switchId))
            throw Errors.InvalidSwitchId;

        var system = await ResolveSystem(systemRef);
        if (system == null)
            throw Errors.SystemNotFound;

        var sw = await _repo.GetSwitchByUuid(switchId);
        if (sw == null || system.Id != sw.System)
            throw Errors.SwitchNotFoundPublic;

        var ctx = ContextFor(system);

        if (!system.FrontHistoryPrivacy.CanAccess(ctx))
            throw Errors.SwitchNotFoundPublic;

        var members = _db.Execute(conn => _repo.GetSwitchMembers(conn, sw.Id));
        return Ok(new FrontersReturnNew
        {
            Uuid = sw.Uuid,
            Timestamp = sw.Timestamp,
            Members = await members.Select(m => m.ToJson(ctx, v: APIVersion.V2)).ToListAsync()
        });
    }

    [HttpPatch("systems/{systemRef}/switches/{switchRef}")]
    public async Task<IActionResult> SwitchPatch(string systemRef, string switchRef, [FromBody] JObject data)
    {
        // for now, don't need to make a PatchObject for this, since it's only one param

        var system = await ResolveSystem(systemRef);
        if (system == null) throw Errors.SystemNotFound;
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        if (!Guid.TryParse(switchRef, out var switchId))
            throw Errors.InvalidSwitchId;

        var valueStr = data.Value<string>("timestamp").NullIfEmpty();
        if (valueStr == null)
            throw new ModelParseError(new List<ValidationError> { new("timestamp", "Key 'timestamp' is required.") });

        var value = Instant.FromDateTimeOffset(DateTime.Parse(valueStr).ToUniversalTime());

        var sw = await _repo.GetSwitchByUuid(switchId);
        if (sw == null || system.Id != sw.System)
            throw Errors.SwitchNotFoundPublic;

        if (await _repo.GetSwitches(system.Id).Select(x => x.Timestamp).ContainsAsync(value))
            throw Errors.SameSwitchTimestampError;

        await _repo.MoveSwitch(sw.Id, value);

        var members = await _db.Execute(conn => _repo.GetSwitchMembers(conn, sw.Id)).ToListAsync();
        return Ok(new FrontersReturnNew
        {
            Uuid = sw.Uuid,
            Timestamp = sw.Timestamp,
            Members = members.Select(x => x.ToJson(LookupContext.ByOwner, v: APIVersion.V2))
        });
    }

    [HttpPatch("systems/{systemRef}/switches/{switchRef}/members")]
    public async Task<IActionResult> SwitchMemberPatch(string systemRef, string switchRef, [FromBody] JArray data)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null) throw Errors.SystemNotFound;
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        if (!Guid.TryParse(switchRef, out var switchId))
            throw Errors.SwitchNotFound;

        if (data.Distinct().Count() != data.Count)
            throw Errors.DuplicateMembersInList;

        var sw = await _repo.GetSwitchByUuid(switchId);
        if (sw == null)
            throw Errors.SwitchNotFound;

        var members = new List<PKMember>();

        foreach (var JmemberRef in data)
        {
            var memberRef = JmemberRef.Value<string>();

            var member = await ResolveMember(memberRef);
            if (member == null)
                throw Errors.MemberNotFoundWithRef(memberRef);
            if (member.System != system.Id)
                throw Errors.NotOwnMemberErrorWithRef(memberRef);

            members.Add(member);
        }

        var latestSwitchMembers = _db.Execute(conn => _repo.GetSwitchMembers(conn, sw.Id));

        if (await latestSwitchMembers.Select(m => m.Hid)
                .SequenceEqualAsync(members.Select(m => m.Hid).ToAsyncEnumerable()))
            throw Errors.SameSwitchMembersError;

        await _db.Execute(conn => _repo.EditSwitch(conn, sw.Id, members.Select(x => x.Id).ToList()));
        return Ok(new FrontersReturnNew
        {
            Uuid = sw.Uuid,
            Timestamp = sw.Timestamp,
            Members = members.Select(x => x.ToJson(LookupContext.ByOwner, v: APIVersion.V2))
        });
    }

    [HttpDelete("systems/{systemRef}/switches/{switchRef}")]
    public async Task<IActionResult> SwitchDelete(string systemRef, string switchRef)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null) throw Errors.SystemNotFound;
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        if (!Guid.TryParse(switchRef, out var switchId))
            throw Errors.InvalidSwitchId;

        var sw = await _repo.GetSwitchByUuid(switchId);
        if (sw == null || system.Id != sw.System)
            throw Errors.SwitchNotFoundPublic;

        await _repo.DeleteSwitch(sw.Id);

        return NoContent();
    }
}