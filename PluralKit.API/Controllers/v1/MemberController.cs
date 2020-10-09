using System.Threading.Tasks;

using Dapper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route( "v{version:apiVersion}/m" )]
    public class MemberController: ControllerBase
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly IAuthorizationService _auth;

        public MemberController(IAuthorizationService auth, IDatabase db, ModelRepository repo)
        {
            _auth = auth;
            _db = db;
            _repo = repo;
        }

        [HttpGet("{hid}")]
        public async Task<ActionResult<JObject>> GetMember(string hid)
        {
            var member = await _db.Execute(conn => _repo.GetMemberByHid(conn, hid));
            if (member == null) return NotFound("Member not found.");

            return Ok(member.ToJson(User.ContextFor(member)));
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult<JObject>> PostMember([FromBody] JObject properties)
        {
            if (!properties.ContainsKey("name"))
                return BadRequest("Member name must be specified.");
            
            var systemId = User.CurrentSystem();

            await using var conn = await _db.Obtain();
            var systemData = await _repo.GetSystem(conn, systemId);

            // Enforce per-system member limit
            var memberCount = await conn.QuerySingleAsync<int>("select count(*) from members where system = @System", new {System = systemId});
            var memberLimit = systemData?.MemberLimitOverride ?? Limits.MaxMemberCount;
            if (memberCount >= memberLimit)
                return BadRequest($"Member limit reached ({memberCount} / {memberLimit}).");

            var member = await _repo.CreateMember(conn, systemId, properties.Value<string>("name"));
            MemberPatch patch;
            try
            {
                patch = JsonModelExt.ToMemberPatch(properties);
            }
            catch (JsonModelParseError e)
            {
                return BadRequest(e.Message);
            }
            
            member = await _repo.UpdateMember(conn, member.Id, patch);
            return Ok(member.ToJson(User.ContextFor(member)));
        }

        [HttpPatch("{hid}")]
        [Authorize]
        public async Task<ActionResult<JObject>> PatchMember(string hid, [FromBody] JObject changes)
        {
            await using var conn = await _db.Obtain();

            var member = await _repo.GetMemberByHid(conn, hid);
            if (member == null) return NotFound("Member not found.");
            
            var res = await _auth.AuthorizeAsync(User, member, "EditMember");
            if (!res.Succeeded) return Unauthorized($"Member '{hid}' is not part of your system.");

            MemberPatch patch;
            try
            {
                patch = JsonModelExt.ToMemberPatch(changes);
            }
            catch (JsonModelParseError e)
            {
                return BadRequest(e.Message);
            }
            
            var newMember = await _repo.UpdateMember(conn, member.Id, patch);
            return Ok(newMember.ToJson(User.ContextFor(newMember)));
        }
        
        [HttpDelete("{hid}")]
        [Authorize]
        public async Task<ActionResult> DeleteMember(string hid)
        {
            await using var conn = await _db.Obtain();

            var member = await _repo.GetMemberByHid(conn, hid);
            if (member == null) return NotFound("Member not found.");
            
            var res = await _auth.AuthorizeAsync(User, member, "EditMember");
            if (!res.Succeeded) return Unauthorized($"Member '{hid}' is not part of your system.");

            await _repo.DeleteMember(conn, member.Id);
            return Ok();
        }
    }
}
