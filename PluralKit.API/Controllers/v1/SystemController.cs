using Dapper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API;

public struct SwitchesReturn
{
    [JsonProperty("timestamp")] public Instant Timestamp { get; set; }
    [JsonProperty("members")] public IEnumerable<string> Members { get; set; }
}

public struct FrontersReturn
{
    [JsonProperty("timestamp")] public Instant Timestamp { get; set; }
    [JsonProperty("members")] public IEnumerable<JObject> Members { get; set; }
}

public struct PostSwitchParams
{
    public Instant? Timestamp { get; set; }
    public ICollection<string> Members { get; set; }
}

[ApiController]
[Route("v1/s")]
public class SystemController: ControllerBase
{
    private readonly IDatabase _db;
    private readonly ModelRepository _repo;
    private readonly IAuthorizationService _auth;

    public SystemController(IDatabase db, IAuthorizationService auth, ModelRepository repo)
    {
        _db = db;
        _auth = auth;
        _repo = repo;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<JObject>> GetOwnSystem()
    {
        var system = await _repo.GetSystem(User.CurrentSystem());
        return system.ToJson(User.ContextFor(system));
    }

    [HttpGet("{hid}")]
    public async Task<ActionResult<JObject>> GetSystem(string hid)
    {
        var system = await _repo.GetSystemByHid(hid);
        if (system == null) return NotFound("System not found.");
        return Ok(system.ToJson(User.ContextFor(system)));
    }

    [HttpGet("{hid}/members")]
    public async Task<ActionResult<IEnumerable<JObject>>> GetMembers(string hid)
    {
        var system = await _repo.GetSystemByHid(hid);
        if (system == null)
            return NotFound("System not found.");

        if (!system.MemberListPrivacy.CanAccess(User.ContextFor(system)))
            return StatusCode(StatusCodes.Status403Forbidden, "Unauthorized to view member list.");

        var members = _repo.GetSystemMembers(system.Id);
        return Ok(await members
            .Where(m => m.MemberVisibility.CanAccess(User.ContextFor(system)))
            .Select(m => m.ToJson(User.ContextFor(system), needsLegacyProxyTags: true))
            .ToListAsync());
    }

    [HttpGet("{hid}/switches")]
    public async Task<ActionResult<IEnumerable<SwitchesReturn>>> GetSwitches(
        string hid, [FromQuery(Name = "before")] Instant? before)
    {
        if (before == null) before = SystemClock.Instance.GetCurrentInstant();

        var system = await _repo.GetSystemByHid(hid);
        if (system == null) return NotFound("System not found.");

        var auth = await _auth.AuthorizeAsync(User, system, "ViewFrontHistory");
        if (!auth.Succeeded)
            return StatusCode(StatusCodes.Status403Forbidden, "Unauthorized to view front history.");

        var res = await _db.Execute(conn => conn.QueryAsync<SwitchesReturn>(
            @"select *, array(
                    select members.hid from switch_members, members
                    where switch_members.switch = switches.id and members.id = switch_members.member
                ) as members from switches
                where switches.system = @System and switches.timestamp < @Before
                order by switches.timestamp desc
                limit 100;",
            new { System = system.Id, Before = before }
        ));

        return Ok(res);
    }

    [HttpGet("{hid}/fronters")]
    public async Task<ActionResult<FrontersReturn>> GetFronters(string hid)
    {
        var system = await _repo.GetSystemByHid(hid);
        if (system == null) return NotFound("System not found.");

        var auth = await _auth.AuthorizeAsync(User, system, "ViewFront");
        if (!auth.Succeeded) return StatusCode(StatusCodes.Status403Forbidden, "Unauthorized to view fronter.");

        var sw = await _repo.GetLatestSwitch(system.Id);
        if (sw == null) return NotFound("System has no registered switches.");

        var members = _db.Execute(conn => _repo.GetSwitchMembers(conn, sw.Id));
        return Ok(new FrontersReturn
        {
            Timestamp = sw.Timestamp,
            Members = await members.Select(m => m.ToJson(User.ContextFor(system), true)).ToListAsync()
        });
    }

    [HttpPatch]
    [Authorize]
    public async Task<ActionResult<JObject>> EditSystem([FromBody] JObject changes)
    {
        var system = await _repo.GetSystem(User.CurrentSystem());

        var patch = SystemPatch.FromJSON(changes);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
        {
            var err = patch.Errors[0];
            if (err is FieldTooLongError)
                return BadRequest($"Field {err.Key} is too long "
                                  + $"({(err as FieldTooLongError).ActualLength} > {(err as FieldTooLongError).MaxLength}).");

            return BadRequest($"Field {err.Key} is invalid.");
        }

        system = await _repo.UpdateSystem(system!.Id, patch);
        return Ok(system.ToJson(User.ContextFor(system)));
    }

    [HttpPost("switches")]
    [Authorize]
    public async Task<IActionResult> PostSwitch([FromBody] PostSwitchParams param)
    {
        if (param.Members.Distinct().Count() != param.Members.Count)
            return BadRequest("Duplicate members in member list.");

        await using var conn = await _db.Obtain();

        // We get the current switch, if it exists
        var latestSwitch = await _repo.GetLatestSwitch(User.CurrentSystem());
        if (latestSwitch != null)
        {
            var latestSwitchMembers = _repo.GetSwitchMembers(conn, latestSwitch.Id);

            // Bail if this switch is identical to the latest one
            if (await latestSwitchMembers.Select(m => m.Hid).SequenceEqualAsync(param.Members.ToAsyncEnumerable()))
                return BadRequest("New members identical to existing fronters.");
        }

        // Resolve member objects for all given IDs
        var membersList =
            (await conn.QueryAsync<PKMember>("select * from members where hid = any(@Hids)",
                new { Hids = param.Members })).ToList();

        foreach (var member in membersList)
            if (member.System != User.CurrentSystem())
                return BadRequest($"Cannot switch to member '{member.Hid}' not in system.");

        // membersList is in DB order, and we want it in actual input order
        // so we go through a dict and map the original input appropriately
        var membersDict = membersList.ToDictionary(m => m.Hid);

        var membersInOrder = new List<PKMember>();
        // We do this without .Select() since we want to have the early return bail if it doesn't find the member
        foreach (var givenMemberId in param.Members)
        {
            if (!membersDict.TryGetValue(givenMemberId, out var member))
                return BadRequest($"Member '{givenMemberId}' not found.");
            membersInOrder.Add(member);
        }

        // Finally, log the switch (yay!)
        await _repo.AddSwitch(conn, User.CurrentSystem(), membersInOrder.Select(m => m.Id).ToList());
        return NoContent();
    }
}