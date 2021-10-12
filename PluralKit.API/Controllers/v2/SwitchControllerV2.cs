using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API
{
    public struct SwitchesReturnNew
    {
        [JsonProperty("timestamp")] public Instant Timestamp { get; set; }
        [JsonProperty("id")] public Guid Uuid { get; set; }
        [JsonProperty("members")] public IEnumerable<string> Members { get; set; }
    }

    [ApiController]
    [ApiVersion("2.0")]
    [Route("v{version:apiVersion}")]
    public class SwitchControllerV2: PKControllerBase
    {
        public SwitchControllerV2(IServiceProvider svc) : base(svc) { }


        [HttpGet("systems/{systemRef}/switches")]
        public async Task<IActionResult> GetSystemSwitches(string systemRef, [FromQuery(Name = "before")] Instant? before, [FromQuery(Name = "limit")] int? limit)
        {
            var system = await ResolveSystem(systemRef);
            if (system == null)
                throw APIErrors.SystemNotFound;

            var ctx = this.ContextFor(system);

            if (!system.FrontHistoryPrivacy.CanAccess(ctx))
                throw APIErrors.UnauthorizedFrontHistory;

            if (before == null)
                before = SystemClock.Instance.GetCurrentInstant();

            if (limit == null || limit > 100)
                limit = 100;

            var res = await _db.Execute(conn => conn.QueryAsync<SwitchesReturnNew>(
                @"select *, array(
                        select members.hid from switch_members, members
                        where switch_members.switch = switches.id and members.id = switch_members.member
                    ) as members from switches
                    where switches.system = @System and switches.timestamp <= @Before
                    order by switches.timestamp desc
                    limit @Limit;", new { System = system.Id, Before = before, Limit = limit }));
            return Ok(res);
        }

        [HttpGet("systems/{systemRef}/fronters")]
        public async Task<IActionResult> GetSystemFronters(string systemRef)
        {
            var system = await ResolveSystem(systemRef);
            if (system == null)
                throw APIErrors.SystemNotFound;

            var ctx = this.ContextFor(system);

            if (!system.FrontPrivacy.CanAccess(ctx))
                throw APIErrors.UnauthorizedCurrentFronters;

            var sw = await _repo.GetLatestSwitch(system.Id);
            if (sw == null)
                return NoContent();

            var members = _db.Execute(conn => _repo.GetSwitchMembers(conn, sw.Id));
            return Ok(new FrontersReturn
            {
                Timestamp = sw.Timestamp,
                Members = await members.Select(m => m.ToJson(ctx, v: APIVersion.V2)).ToListAsync()
            });
        }


        [HttpPost("systems/{system}/switches")]
        public async Task<IActionResult> SwitchCreate(string system, [FromBody] JObject data)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }


        [HttpGet("systems/{systemRef}/switches/{switchRef}")]
        public async Task<IActionResult> SwitchGet(string systemRef, string switchRef)
        {
            if (!Guid.TryParse(switchRef, out var switchId))
                throw APIErrors.SwitchNotFound;

            var system = await ResolveSystem(systemRef);
            if (system == null)
                throw APIErrors.SystemNotFound;

            var sw = await _repo.GetSwitchByUuid(switchId);
            if (sw == null || system.Id != sw.System)
                throw APIErrors.SwitchNotFound;

            var ctx = this.ContextFor(system);

            if (!system.FrontHistoryPrivacy.CanAccess(ctx))
                throw APIErrors.SwitchNotFound;

            var members = _db.Execute(conn => _repo.GetSwitchMembers(conn, sw.Id));
            return Ok(new FrontersReturn
            {
                Timestamp = sw.Timestamp,
                Members = await members.Select(m => m.ToJson(ctx, v: APIVersion.V2)).ToListAsync()
            });
        }

        [HttpPatch("systems/{system}/switches/{switch_id}")]
        public async Task<IActionResult> SwitchPatch(string system, [FromBody] JObject data)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

        [HttpDelete("systems/{system}/switches/{switch_id}")]
        public async Task<IActionResult> SwitchDelete(string system, string switch_id)
        {
            return new ObjectResult("Unimplemented")
            {
                StatusCode = 501
            };
        }

    }
}