using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("m")]
    [Route( "v{version:apiVersion}/m" )]
    public class MemberController: ControllerBase
    {
        private IDataStore _data;
        private TokenAuthService _auth;

        public MemberController(IDataStore data, TokenAuthService auth)
        {
            _data = data;
            _auth = auth;
        }

        [HttpGet("{hid}")]
        public async Task<ActionResult<JObject>> GetMember(string hid)
        {
            var member = await _data.GetMemberByHid(hid);
            if (member == null) return NotFound("Member not found.");

            return Ok(member.ToJson(_auth.ContextFor(member)));
        }

        [HttpPost]
        [RequiresSystem]
        public async Task<ActionResult<JObject>> PostMember([FromBody] JObject properties)
        {
            var system = _auth.CurrentSystem;

            if (!properties.ContainsKey("name"))
                return BadRequest("Member name must be specified.");

            // Enforce per-system member limit
            var memberCount = await _data.GetSystemMemberCount(system, true);
            if (memberCount >= Limits.MaxMemberCount)
                return BadRequest($"Member limit reached ({memberCount} / {Limits.MaxMemberCount}).");

            var member = await _data.CreateMember(system, properties.Value<string>("name"));
            try
            {
                member.ApplyJson(properties);
            }
            catch (JsonModelParseError e)
            {
                return BadRequest(e.Message);
            }
            
            await _data.SaveMember(member);
            return Ok(member.ToJson(_auth.ContextFor(member)));
        }

        [HttpPatch("{hid}")]
        [RequiresSystem]
        public async Task<ActionResult<JObject>> PatchMember(string hid, [FromBody] JObject changes)
        {
            var member = await _data.GetMemberByHid(hid);
            if (member == null) return NotFound("Member not found.");

            if (member.System != _auth.CurrentSystem.Id) return Unauthorized($"Member '{hid}' is not part of your system.");

            try
            {
                member.ApplyJson(changes);
            }
            catch (JsonModelParseError e)
            {
                return BadRequest(e.Message);
            }
            
            await _data.SaveMember(member);
            return Ok(member.ToJson(_auth.ContextFor(member)));
        }
        
        [HttpDelete("{hid}")]
        [RequiresSystem]
        public async Task<ActionResult> DeleteMember(string hid)
        {
            var member = await _data.GetMemberByHid(hid);
            if (member == null) return NotFound("Member not found.");
            
            if (member.System != _auth.CurrentSystem.Id) return Unauthorized($"Member '{hid}' is not part of your system.");
            
            await _data.DeleteMember(member);
            return Ok();
        }
    }
}
