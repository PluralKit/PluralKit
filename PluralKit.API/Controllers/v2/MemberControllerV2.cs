using System.Net;
using System.Net.Http;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

[ApiController]
[Route("v2")]
public class MemberControllerV2: PKControllerBase
{
    public MemberControllerV2(IServiceProvider svc) : base(svc) { }


    [HttpGet("systems/{systemRef}/members")]
    public async Task<IActionResult> GetSystemMembers(string systemRef)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null)
            throw Errors.SystemNotFound;

        var ctx = ContextFor(system);

        if (!system.MemberListPrivacy.CanAccess(ContextFor(system)))
            throw Errors.UnauthorizedMemberList;

        var members = _repo.GetSystemMembers(system.Id);
        return Ok(await members
            .Where(m => m.MemberVisibility.CanAccess(ctx))
            .Select(m => m.ToJson(ctx, systemStr: system.Hid))
            .ToListAsync());
    }

    [HttpPost("members")]
    public async Task<IActionResult> MemberCreate([FromBody] JObject data)
    {
        var system = await ResolveSystem("@me");
        var config = await _repo.GetSystemConfig(system.Id);

        var memberCount = await _repo.GetSystemMemberCount(system.Id);
        var memberLimit = config.MemberLimitOverride ?? Limits.MaxMemberCount;
        if (memberCount >= memberLimit)
            throw Errors.MemberLimitReached;

        var patch = MemberPatch.FromJSON(data);
        patch.AssertIsValid();
        if (!patch.Name.IsPresent)
            patch.Errors.Add(new ValidationError("name", "Key 'name' is required when creating new member."));
        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        if (patch.AvatarUrl.Value != null)
            patch.AvatarUrl = await TryUploadAvatar(patch.AvatarUrl.Value, system);

        using var conn = await _db.Obtain();
        using var tx = await conn.BeginTransactionAsync();

        var newMember = await _repo.CreateMember(system.Id, patch.Name.Value, conn);
        newMember = await _repo.UpdateMember(newMember.Id, patch, conn);

        _ = _dispatch.Dispatch(newMember.Id, new()
        {
            Event = DispatchEvent.CREATE_MEMBER,
            EventData = patch.ToJson(),
        });

        await tx.CommitAsync();

        return Ok(newMember.ToJson(LookupContext.ByOwner, systemStr: system.Hid));
    }

    [HttpGet("members/{memberRef}")]
    public async Task<IActionResult> MemberGet(string memberRef)
    {
        var member = await ResolveMember(memberRef);
        if (member == null)
            throw Errors.MemberNotFound;

        var system = await _repo.GetSystem(member.System);

        return Ok(member.ToJson(ContextFor(member), systemStr: system.Hid));
    }

    [HttpGet("members/{memberRef}/oembed.json")]
    public async Task<IActionResult> MemberEmbed(string memberRef)
    {
        var member = await ResolveMember(memberRef);
        if (member == null)
            throw Errors.MemberNotFound;
        var system = await _repo.GetSystem(member.System);

        var name = member.DisplayName ?? member.Name;
        if (system.Name != null)
            name += $" ({system.Name})";

        return Ok(APIJsonExt.EmbedJson(name, "Member"));
    }

    [HttpPatch("members/{memberRef}")]
    public async Task<IActionResult> DoMemberPatch(string memberRef, [FromBody] JObject data)
    {
        var system = await ResolveSystem("@me");
        var member = await ResolveMember(memberRef);
        if (member == null)
            throw Errors.MemberNotFound;
        if (member.System != system.Id)
            throw Errors.NotOwnMemberError;

        var patch = MemberPatch.FromJSON(data);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        if (patch.AvatarUrl.Value != null)
            patch.AvatarUrl = await TryUploadAvatar(patch.AvatarUrl.Value, system);

        var newMember = await _repo.UpdateMember(member.Id, patch);
        return Ok(newMember.ToJson(LookupContext.ByOwner, systemStr: system.Hid));
    }

    [HttpDelete("members/{memberRef}")]
    public async Task<IActionResult> MemberDelete(string memberRef)
    {
        var member = await ResolveMember(memberRef);
        if (member == null)
            throw Errors.MemberNotFound;

        var system = await ResolveSystem("@me");
        if (system.Id != member.System)
            throw Errors.NotOwnMemberError;

        await _repo.DeleteMember(member.Id);

        return NoContent();
    }

    private async Task<string> TryUploadAvatar(string avatarUrl, PKSystem system)
    {
        if (!avatarUrl.StartsWith("https://serve.apparyllis.com/")) return avatarUrl;
        if (_config.AvatarServiceUrl == null) return avatarUrl;
        if (!HttpContext.Items.TryGetValue("AppId", out var appId) || (int)appId != 1) return avatarUrl;

        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync(_config.AvatarServiceUrl + "/pull",
            new { url = avatarUrl, kind = "avatar", uploaded_by = (string)null, system_id = system.Uuid.ToString() });

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            throw new PKError(500, 0, $"Error uploading image to CDN: {error.Error}");
        }

        var success = await response.Content.ReadFromJsonAsync<SuccessResponse>();
        return success.Url;
    }

    public record ErrorResponse(string Error);

    public record SuccessResponse(string Url, bool New);
}