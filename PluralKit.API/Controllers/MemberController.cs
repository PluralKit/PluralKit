using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PluralKit.Core;

namespace PluralKit.API.Controllers
{
    [ApiController]
    [Route("m")]
    [Route("v1/m")]
    public class MemberController: ControllerBase
    {
        private MemberStore _members;
        private DbConnectionFactory _conn;
        private TokenAuthService _auth;

        public MemberController(MemberStore members, DbConnectionFactory conn, TokenAuthService auth)
        {
            _members = members;
            _conn = conn;
            _auth = auth;
        }

        [HttpGet("{hid}")]
        public async Task<ActionResult<PKMember>> GetMember(string hid)
        {
            var member = await _members.GetByHid(hid);
            if (member == null) return NotFound("Member not found.");

            return Ok(member);
        }

        [HttpPost]
        [RequiresSystem]
        public async Task<ActionResult<PKMember>> PostMember([FromBody] PKMember newMember)
        {
            var system = _auth.CurrentSystem;

            if (newMember.Name == null)
            return BadRequest("Member name cannot be null.");

            // Enforce per-system member limit
            var memberCount = await _members.MemberCount(system);
            if (memberCount >= Limits.MaxMemberCount)
                return BadRequest($"Member limit reached ({memberCount} / {Limits.MaxMemberCount}).");

            // Explicit bounds checks
            if (newMember.Name != null && newMember.Name.Length > Limits.MaxMemberNameLength)
                return BadRequest($"Member name too long ({newMember.Name.Length} > {Limits.MaxMemberNameLength}.");
            if (newMember.DisplayName != null && newMember.DisplayName.Length > Limits.MaxMemberNameLength)
                return BadRequest($"Member display name too long ({newMember.DisplayName.Length} > {Limits.MaxMemberNameLength}.");
            if (newMember.Pronouns != null && newMember.Pronouns.Length > Limits.MaxPronounsLength)
                return BadRequest($"Member pronouns too long ({newMember.Pronouns.Length} > {Limits.MaxPronounsLength}.");
            if (newMember.Description != null && newMember.Description.Length > Limits.MaxDescriptionLength)
                return BadRequest($"Member descriptions too long ({newMember.Description.Length} > {Limits.MaxDescriptionLength}.");

            // Sanity bounds checks
            if (newMember.AvatarUrl != null && newMember.AvatarUrl.Length > 1000)
                return BadRequest();
            if (newMember.Prefix != null && newMember.Prefix.Length > 1000)
                return BadRequest();
            if (newMember.Suffix != null && newMember.Suffix.Length > 1000)
                return BadRequest();

            var member = await _members.Create(system, newMember.Name);

            member.Name = newMember.Name;
            member.DisplayName = newMember.DisplayName;
            member.Color = newMember.Color;
            member.AvatarUrl = newMember.AvatarUrl;
            member.Birthday = newMember.Birthday;
            member.Pronouns = newMember.Pronouns;
            member.Description = newMember.Description;
            member.Prefix = newMember.Prefix;
            member.Suffix = newMember.Suffix;
            await _members.Save(member);

            return Ok(member);
        }

        [HttpPatch("{hid}")]
        [RequiresSystem]
        public async Task<ActionResult<PKMember>> PatchMember(string hid, [FromBody] PKMember newMember)
        {
            var member = await _members.GetByHid(hid);
            if (member == null) return NotFound("Member not found.");

            if (member.System != _auth.CurrentSystem.Id) return Unauthorized($"Member '{hid}' is not part of your system.");

            if (newMember.Name == null)
                return BadRequest("Member name can not be null.");

            // Explicit bounds checks
            if (newMember.Name != null && newMember.Name.Length > Limits.MaxMemberNameLength)
                return BadRequest($"Member name too long ({newMember.Name.Length} > {Limits.MaxMemberNameLength}.");
            if (newMember.DisplayName != null && newMember.DisplayName.Length > Limits.MaxMemberNameLength)
                return BadRequest($"Member display name too long ({newMember.DisplayName.Length} > {Limits.MaxMemberNameLength}.");
            if (newMember.Pronouns != null && newMember.Pronouns.Length > Limits.MaxPronounsLength)
                return BadRequest($"Member pronouns too long ({newMember.Pronouns.Length} > {Limits.MaxPronounsLength}.");
            if (newMember.Description != null && newMember.Description.Length > Limits.MaxDescriptionLength)
                return BadRequest($"Member descriptions too long ({newMember.Description.Length} > {Limits.MaxDescriptionLength}.");

            // Sanity bounds checks
            if (newMember.AvatarUrl != null && newMember.AvatarUrl.Length > 1000)
                return BadRequest();
            if (newMember.Prefix != null && newMember.Prefix.Length > 1000)
                return BadRequest();
            if (newMember.Suffix != null && newMember.Suffix.Length > 1000)
                return BadRequest();

            member.Name = newMember.Name;
            member.DisplayName = newMember.DisplayName;
            member.Color = newMember.Color;
            member.AvatarUrl = newMember.AvatarUrl;
            member.Birthday = newMember.Birthday;
            member.Pronouns = newMember.Pronouns;
            member.Description = newMember.Description;
            member.Prefix = newMember.Prefix;
            member.Suffix = newMember.Suffix;
            await _members.Save(member);

            return Ok(member);
        }
        
        [HttpDelete("{hid}")]
        [RequiresSystem]
        public async Task<ActionResult<PKMember>> DeleteMember(string hid)
        {
            var member = await _members.GetByHid(hid);
            if (member == null) return NotFound("Member not found.");
            
            if (member.System != _auth.CurrentSystem.Id) return Unauthorized($"Member '{hid}' is not part of your system.");
            
            await _members.Delete(member);

            return Ok();
        }
    }
}
