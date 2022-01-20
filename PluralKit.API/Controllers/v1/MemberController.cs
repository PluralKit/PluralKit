using Dapper;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

[ApiController]
[Route("v1/m")]
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
        var member = await _repo.GetMemberByHid(hid);
        if (member == null) return NotFound("Member not found.");

        return Ok(member.ToJson(User.ContextFor(member), true));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<JObject>> PostMember([FromBody] JObject properties)
    {
        if (!properties.ContainsKey("name"))
            return BadRequest("Member name must be specified.");

        var systemId = User.CurrentSystem();
        var config = await _repo.GetSystemConfig(systemId);

        await using var conn = await _db.Obtain();

        // Enforce per-system member limit
        var memberCount = await conn.QuerySingleAsync<int>("select count(*) from members where system = @System",
            new { System = systemId });
        var memberLimit = config.MemberLimitOverride ?? Limits.MaxMemberCount;
        if (memberCount >= memberLimit)
            return BadRequest($"Member limit reached ({memberCount} / {memberLimit}).");

        await using var tx = await conn.BeginTransactionAsync();
        var member = await _repo.CreateMember(systemId, properties.Value<string>("name"), conn);

        var patch = MemberPatch.FromJSON(properties);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
        {
            await tx.RollbackAsync();

            var err = patch.Errors[0];
            if (err is FieldTooLongError)
                return BadRequest($"Field {err.Key} is too long "
                                  + $"({(err as FieldTooLongError).ActualLength} > {(err as FieldTooLongError).MaxLength}).");
            if (err.Text != null)
                return BadRequest(err.Text);
            return BadRequest($"Field {err.Key} is invalid.");
        }

        member = await _repo.UpdateMember(member.Id, patch, conn);
        await tx.CommitAsync();
        return Ok(member.ToJson(User.ContextFor(member), true));
    }

    [HttpPatch("{hid}")]
    [Authorize]
    public async Task<ActionResult<JObject>> PatchMember(string hid, [FromBody] JObject changes)
    {
        var member = await _repo.GetMemberByHid(hid);
        if (member == null) return NotFound("Member not found.");

        var res = await _auth.AuthorizeAsync(User, member, "EditMember");
        if (!res.Succeeded) return Unauthorized($"Member '{hid}' is not part of your system.");

        var patch = MemberPatch.FromJSON(changes);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
        {
            var err = patch.Errors[0];
            if (err is FieldTooLongError)
                return BadRequest($"Field {err.Key} is too long "
                                  + $"({(err as FieldTooLongError).ActualLength} > {(err as FieldTooLongError).MaxLength}).");
            if (err.Text != null)
                return BadRequest(err.Text);
            return BadRequest($"Field {err.Key} is invalid.");
        }

        var newMember = await _repo.UpdateMember(member.Id, patch);
        return Ok(newMember.ToJson(User.ContextFor(newMember), true));
    }

    [HttpDelete("{hid}")]
    [Authorize]
    public async Task<ActionResult> DeleteMember(string hid)
    {
        var member = await _repo.GetMemberByHid(hid);
        if (member == null) return NotFound("Member not found.");

        var res = await _auth.AuthorizeAsync(User, member, "EditMember");
        if (!res.Succeeded) return Unauthorized($"Member '{hid}' is not part of your system.");

        await _repo.DeleteMember(member.Id);
        return Ok();
    }
}