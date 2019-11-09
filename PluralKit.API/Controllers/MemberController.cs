using System.Linq;
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
        private IDataStore _data;
        private TokenAuthService _auth;

        public MemberController(IDataStore data, TokenAuthService auth)
        {
            _data = data;
            _auth = auth;
        }

        [HttpGet("{hid}")]
        public async Task<ActionResult<PKMember>> GetMember(string hid)
        {
            var member = await _data.GetMemberByHid(hid);
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
            var memberCount = await _data.GetSystemMemberCount(system);
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
            if (newMember.ProxyTags?.Any(tag => (tag.Prefix?.Length ?? 0) > 1000 || (tag.Suffix?.Length ?? 0) > 1000) ?? false)
                return BadRequest();

            var member = await _data.CreateMember(system, newMember.Name);

            member.Name = newMember.Name;
            member.DisplayName = newMember.DisplayName;
            member.Color = newMember.Color;
            member.AvatarUrl = newMember.AvatarUrl;
            member.Birthday = newMember.Birthday;
            member.Pronouns = newMember.Pronouns;
            member.Description = newMember.Description;
            member.ProxyTags = newMember.ProxyTags;
            member.KeepProxy = newMember.KeepProxy;
            await _data.SaveMember(member);

            return Ok(member);
        }

        [HttpPatch("{hid}")]
        [RequiresSystem]
        public async Task<ActionResult<PKMember>> PatchMember(string hid, [FromBody] PKMember newMember)
        {
            var member = await _data.GetMemberByHid(hid);
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
            if (newMember.ProxyTags?.Any(tag => (tag.Prefix?.Length ?? 0) > 1000 || (tag.Suffix?.Length ?? 0) > 1000) ?? false)
                return BadRequest();

            member.Name = newMember.Name;
            member.DisplayName = newMember.DisplayName;
            member.Color = newMember.Color;
            member.AvatarUrl = newMember.AvatarUrl;
            member.Birthday = newMember.Birthday;
            member.Pronouns = newMember.Pronouns;
            member.Description = newMember.Description;
            member.ProxyTags = newMember.ProxyTags;
            member.KeepProxy = newMember.KeepProxy;
            await _data.SaveMember(member);

            return Ok(member);
        }
        
        [HttpDelete("{hid}")]
        [RequiresSystem]
        public async Task<ActionResult<PKMember>> DeleteMember(string hid)
        {
            var member = await _data.GetMemberByHid(hid);
            if (member == null) return NotFound("Member not found.");
            
            if (member.System != _auth.CurrentSystem.Id) return Unauthorized($"Member '{hid}' is not part of your system.");
            
            await _data.DeleteMember(member);

            return Ok();
        }
    }
}
