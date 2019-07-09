using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NodaTime;

namespace PluralKit.API.Controllers
{
    public struct SwitchesReturn
    {
        [JsonProperty("timestamp")] public Instant Timestamp { get; set; }
        [JsonProperty("members")] public IEnumerable<string> Members { get; set; }
    }

    public struct FrontersReturn
    {
        [JsonProperty("timestamp")] public Instant Timestamp { get; set; }
        [JsonProperty("members")] public IEnumerable<PKMember> Members { get; set; }
    }

    public struct PostSwitchParams
    {
        public ICollection<string> Members { get; set; }
    }

    [ApiController]
    [Route("s")]
    public class SystemController : ControllerBase
    {
        private SystemStore _systems;
        private MemberStore _members;
        private SwitchStore _switches;
        private IDbConnection _conn;
        private TokenAuthService _auth;

        public SystemController(SystemStore systems, MemberStore members, SwitchStore switches, IDbConnection conn, TokenAuthService auth)
        {
            _systems = systems;
            _members = members;
            _switches = switches;
            _conn = conn;
            _auth = auth;
        }

        [HttpGet("{hid}")]
        public async Task<ActionResult<PKSystem>> GetSystem(string hid)
        {
            var system = await _systems.GetByHid(hid);
            if (system == null) return NotFound("System not found.");
            return Ok(system);
        }

        [HttpGet("{hid}/members")]
        public async Task<ActionResult<IEnumerable<PKMember>>> GetMembers(string hid)
        {
            var system = await _systems.GetByHid(hid);
            if (system == null) return NotFound("System not found.");

            var members = await _members.GetBySystem(system);
            return Ok(members);
        }

        [HttpGet("{hid}/switches")]
        public async Task<ActionResult<IEnumerable<SwitchesReturn>>> GetSwitches(string hid, [FromQuery(Name = "before")] Instant? before)
        {
            if (before == default(Instant)) before = SystemClock.Instance.GetCurrentInstant();
            
            var system = await _systems.GetByHid(hid);
            if (system == null) return NotFound("System not found.");

            var res = await _conn.QueryAsync<SwitchesReturn>(
                @"select *, array(
                        select members.hid from switch_members, members
                        where switch_members.switch = switches.id and members.id = switch_members.member
                    ) as members from switches
                    where switches.system = @System and switches.timestamp < @Before
                    order by switches.timestamp desc
                    limit 100;", new { System = system.Id, Before = before });
            return Ok(res);
        }

        [HttpGet("{hid}/fronters")]
        public async Task<ActionResult<FrontersReturn>> GetFronters(string hid)
        {
            var system = await _systems.GetByHid(hid);
            if (system == null) return NotFound("System not found.");
            
            var sw = await _switches.GetLatestSwitch(system);
            var members = await _switches.GetSwitchMembers(sw);
            return Ok(new FrontersReturn
            {
                Timestamp = sw.Timestamp,
                Members = members
            });
        }

        [HttpPatch]
        [RequiresSystem]
        public async Task<ActionResult<PKSystem>> EditSystem([FromBody] PKSystem newSystem)
        {
            var system = _auth.CurrentSystem;
            
            system.Name = newSystem.Name;
            system.Description = newSystem.Description;
            system.Tag = newSystem.Tag;
            system.AvatarUrl = newSystem.AvatarUrl;
            system.UiTz = newSystem.UiTz ?? "UTC";
            
            await _systems.Save(system);
            return Ok(system);
        }

        [HttpPost("switches")]
        [RequiresSystem]
        public async Task<IActionResult> PostSwitch([FromBody] PostSwitchParams param)
        {
            if (param.Members.Distinct().Count() != param.Members.Count())
                return BadRequest("Duplicate members in member list.");
            
            // We get the current switch, if it exists
            var latestSwitch = await _switches.GetLatestSwitch(_auth.CurrentSystem);
            var latestSwitchMembers = await _switches.GetSwitchMembers(latestSwitch);

            // Bail if this switch is identical to the latest one
            if (latestSwitchMembers.Select(m => m.Hid).SequenceEqual(param.Members))
                return BadRequest("New members identical to existing fronters.");
            
            // Resolve member objects for all given IDs
            var membersList = (await _conn.QueryAsync<PKMember>("select * from members where hid = any(@Hids)", new {Hids = param.Members})).ToList();
            foreach (var member in membersList)
                if (member.System != _auth.CurrentSystem.Id)
                    return BadRequest($"Cannot switch to member '{member.Hid}' not in system.");

            // membersList is in DB order, and we want it in actual input order
            // so we go through a dict and map the original input appropriately
            var membersDict = membersList.ToDictionary(m => m.Hid);
            
            var membersInOrder = new List<PKMember>();
            // We do this without .Select() since we want to have the early return bail if it doesn't find the member
            foreach (var givenMemberId in param.Members)
            {
                if (!membersDict.TryGetValue(givenMemberId, out var member)) return BadRequest($"Member '{givenMemberId}' not found.");
                membersInOrder.Add(member);
            }

            // Finally, log the switch (yay!)
            await _switches.RegisterSwitch(_auth.CurrentSystem, membersInOrder);
            return NoContent();
        }
    }
}