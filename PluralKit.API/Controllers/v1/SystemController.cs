using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API
{
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
        public ICollection<string> Members { get; set; }
    }

    [ApiController]
    [ApiVersion("1.0")]
    [Route("s")]
    [Route( "v{version:apiVersion}/s" )]
    public class SystemController : ControllerBase
    {
        private IDataStore _data;
        private IDatabase _conn;
        private IAuthorizationService _auth;

        public SystemController(IDataStore data, IDatabase conn, IAuthorizationService auth)
        {
            _data = data;
            _conn = conn;
            _auth = auth;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult<JObject>> GetOwnSystem()
        {
            var system = await _conn.Execute(c => c.QuerySystem(User.CurrentSystem()));
            return system.ToJson(User.ContextFor(system));
        }

        [HttpGet("{hid}")]
        public async Task<ActionResult<JObject>> GetSystem(string hid)
        {
            var system = await _data.GetSystemByHid(hid);
            if (system == null) return NotFound("System not found.");
            return Ok(system.ToJson(User.ContextFor(system)));
        }

        [HttpGet("{hid}/members")]
        public async Task<ActionResult<IEnumerable<JObject>>> GetMembers(string hid)
        {
            var system = await _data.GetSystemByHid(hid);
            if (system == null) return NotFound("System not found.");

            if (!system.MemberListPrivacy.CanAccess(User.ContextFor(system)))
                return StatusCode(StatusCodes.Status403Forbidden, "Unauthorized to view member list.");

            var members = _data.GetSystemMembers(system);
            return Ok(await members
                .Where(m => m.MemberVisibility.CanAccess(User.ContextFor(system)))
                .Select(m => m.ToJson(User.ContextFor(system)))
                .ToListAsync());
        }

        [HttpGet("{hid}/switches")]
        public async Task<ActionResult<IEnumerable<SwitchesReturn>>> GetSwitches(string hid, [FromQuery(Name = "before")] Instant? before)
        {
            if (before == null) before = SystemClock.Instance.GetCurrentInstant();
            
            var system = await _data.GetSystemByHid(hid);
            if (system == null) return NotFound("System not found.");

            var auth = await _auth.AuthorizeAsync(User, system, "ViewFrontHistory");
            if (!auth.Succeeded) return StatusCode(StatusCodes.Status403Forbidden, "Unauthorized to view front history.");

            using (var conn = await _conn.Obtain())
            {
                var res = await conn.QueryAsync<SwitchesReturn>(
                    @"select *, array(
                        select members.hid from switch_members, members
                        where switch_members.switch = switches.id and members.id = switch_members.member
                    ) as members from switches
                    where switches.system = @System and switches.timestamp < @Before
                    order by switches.timestamp desc
                    limit 100;", new {System = system.Id, Before = before});
                return Ok(res);
            }
        }

        [HttpGet("{hid}/fronters")]
        public async Task<ActionResult<FrontersReturn>> GetFronters(string hid)
        {
            var system = await _data.GetSystemByHid(hid);
            if (system == null) return NotFound("System not found.");
            
            var auth = await _auth.AuthorizeAsync(User, system, "ViewFront");
            if (!auth.Succeeded) return StatusCode(StatusCodes.Status403Forbidden, "Unauthorized to view fronter.");
            
            var sw = await _data.GetLatestSwitch(system.Id);
            if (sw == null) return NotFound("System has no registered switches."); 
                
            var members = _data.GetSwitchMembers(sw);
            return Ok(new FrontersReturn
            {
                Timestamp = sw.Timestamp,
                Members = await members.Select(m => m.ToJson(User.ContextFor(system))).ToListAsync()
            });
        }

        [HttpPatch]
        [Authorize]
        public async Task<ActionResult<JObject>> EditSystem([FromBody] JObject changes)
        {
            var system = await _conn.Execute(c => c.QuerySystem(User.CurrentSystem()));
            try
            {
                system.ApplyJson(changes);
            }
            catch (JsonModelParseError e)
            {
                return BadRequest(e.Message);
            }

            await _data.SaveSystem(system);
            return Ok(system.ToJson(User.ContextFor(system)));
        }

        [HttpPost("switches")]
        [Authorize]
        public async Task<IActionResult> PostSwitch([FromBody] PostSwitchParams param)
        {
            if (param.Members.Distinct().Count() != param.Members.Count)
                return BadRequest("Duplicate members in member list.");
            
            // We get the current switch, if it exists
            var latestSwitch = await _data.GetLatestSwitch(User.CurrentSystem());
            if (latestSwitch != null)
            {
                var latestSwitchMembers = _data.GetSwitchMembers(latestSwitch);

                // Bail if this switch is identical to the latest one
                if (await latestSwitchMembers.Select(m => m.Hid).SequenceEqualAsync(param.Members.ToAsyncEnumerable()))
                    return BadRequest("New members identical to existing fronters.");
            }

            // Resolve member objects for all given IDs
            IEnumerable<PKMember> membersList;
            using (var conn = await _conn.Obtain())
                membersList = (await conn.QueryAsync<PKMember>("select * from members where hid = any(@Hids)", new {Hids = param.Members})).ToList();
            
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
                if (!membersDict.TryGetValue(givenMemberId, out var member)) return BadRequest($"Member '{givenMemberId}' not found.");
                membersInOrder.Add(member);
            }

            // Finally, log the switch (yay!)
            await _data.AddSwitch(User.CurrentSystem(), membersInOrder);
            return NoContent();
        }
    }
}