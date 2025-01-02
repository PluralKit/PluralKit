using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

[ApiController]
[Route("v2")]
public class AutoproxyControllerV2: PKControllerBase
{
    public AutoproxyControllerV2(IServiceProvider svc) : base(svc) { }

    // asp.net why

    [HttpGet("systems/{systemRef}/autoproxy")]
    public Task<IActionResult> GetWrapper([FromRoute] string systemRef, [FromQuery] ulong? guild_id, [FromQuery] ulong? channel_id)
        => Entrypoint(systemRef, guild_id, channel_id, null);

    [HttpPatch("systems/{systemRef}/autoproxy")]
    public Task<IActionResult> PatchWrapper([FromRoute] string systemRef, [FromQuery] ulong? guild_id, [FromQuery] ulong? channel_id, [FromBody] JObject? data)
        => Entrypoint(systemRef, guild_id, channel_id, data);

    public async Task<IActionResult> Entrypoint(string systemRef, ulong? guild_id, ulong? channel_id, JObject? data)
    {
        var system = await ResolveSystem(systemRef);
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        if (guild_id == null || channel_id != null)
            throw Errors.Unimplemented;

        var settings = await _repo.GetAutoproxySettings(system.Id, guild_id, channel_id);
        if (settings == null)
            return NotFound();

        if (HttpContext.Request.Method == "GET")
            return await Get(settings);
        else if (HttpContext.Request.Method == "PATCH")
            return await Patch(system, guild_id, channel_id, data, settings);
        else return StatusCode(415);
    }

    private async Task<IActionResult> Get(AutoproxySettings settings)
    {
        string hid = null;
        if (settings.AutoproxyMember != null)
            hid = (await _repo.GetMember(settings.AutoproxyMember.Value))?.Hid;

        return Ok(settings.ToJson(hid));
    }

    private async Task<IActionResult> Patch(PKSystem system, ulong? guildId, ulong? channelId, JObject data, AutoproxySettings oldData)
    {
        var updateMember = data.ContainsKey("autoproxy_member") && data.Value<string>("autoproxy_member") != null;

        PKMember? member = null;
        if (updateMember)
        {
            member = await ResolveMember(data.Value<string>("autoproxy_member"));
            if (member != null && ContextFor(member) != LookupContext.ByOwner)
                throw Errors.GenericMissingPermissions;
        }

        var patch = AutoproxyPatch.FromJson(data, member?.Id);
        patch.AssertIsValid();

        var newAutoproxyMode = patch.AutoproxyMode.IsPresent ? patch.AutoproxyMode : oldData.AutoproxyMode;
        var newAutoproxyMember = patch.AutoproxyMember.IsPresent ? patch.AutoproxyMember : oldData.AutoproxyMember;

        if (updateMember && member == null)
        {
            patch.Errors.Add(new("autoproxy_member", "Member not found."));
        }

        // only allow setting member for latch (or member)
        if ((int)newAutoproxyMode.Value < 3)
        {
            if (updateMember)
            {
                patch.Errors.Add(new("autoproxy_member", "Cannot update autoproxy member if autoproxy is disabled or set to 'front' mode"));
            }

            patch.AutoproxyMember = null;
        }

        if (newAutoproxyMode.Value == AutoproxyMode.Member && newAutoproxyMember.Value == null)
        {
            patch.Errors.Add(new("autoproxy_member", "An autoproxy member must be supplied for autoproxy mode 'member'"));
        }

        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        var res = await _repo.UpdateAutoproxy(system.Id, guildId, channelId, patch);
        if (!updateMember && oldData.AutoproxyMember != null)
            member = await _repo.GetMember(oldData.AutoproxyMember.Value);
        return Ok(res.ToJson(member?.Hid));
    }
}