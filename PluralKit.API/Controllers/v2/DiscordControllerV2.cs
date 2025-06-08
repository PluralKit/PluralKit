using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

[ApiController]
[Route("v2")]
public class DiscordControllerV2: PKControllerBase
{
    public DiscordControllerV2(IServiceProvider svc) : base(svc) { }


    [HttpGet("systems/{systemRef}/guilds/{guild_id}")]
    public async Task<IActionResult> SystemGuildGet(string systemRef, ulong guild_id)
    {
        var system = await ResolveSystem(systemRef);
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        var settings = await _repo.GetSystemGuild(guild_id, system.Id, false, _config.SearchGuildSettings);
        if (settings == null)
            throw Errors.SystemGuildNotFound;

        return Ok(settings.ToJson());
    }

    [HttpPatch("systems/{systemRef}/guilds/{guild_id}")]
    public async Task<IActionResult> DoSystemGuildPatch(string systemRef, ulong guild_id, [FromBody] JObject data)
    {
        var system = await ResolveSystem(systemRef);
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        var settings = await _repo.GetSystemGuild(guild_id, system.Id, false, _config.SearchGuildSettings);
        if (settings == null)
            throw Errors.SystemGuildNotFound;

        var patch = SystemGuildPatch.FromJson(data);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        var newSettings = await _repo.UpdateSystemGuild(system.Id, guild_id, patch);
        return Ok(newSettings.ToJson());
    }

    [HttpGet("members/{memberRef}/guilds/{guild_id}")]
    public async Task<IActionResult> MemberGuildGet(string memberRef, ulong guild_id)
    {
        var system = await ResolveSystem("@me");
        var member = await ResolveMember(memberRef);
        if (member == null)
            throw Errors.MemberNotFound;
        if (member.System != system.Id)
            throw Errors.NotOwnMemberError;

        var settings = await _repo.GetMemberGuild(guild_id, member.Id, false, _config.SearchGuildSettings ? system.Id : null);
        if (settings == null)
            throw Errors.MemberGuildNotFound;

        return Ok(settings.ToJson());
    }

    [HttpPatch("members/{memberRef}/guilds/{guild_id}")]
    public async Task<IActionResult> DoMemberGuildPatch(string memberRef, ulong guild_id, [FromBody] JObject data)
    {
        var system = await ResolveSystem("@me");
        var member = await ResolveMember(memberRef);
        if (member == null)
            throw Errors.MemberNotFound;
        if (member.System != system.Id)
            throw Errors.NotOwnMemberError;

        var settings = await _repo.GetMemberGuild(guild_id, member.Id, false, _config.SearchGuildSettings ? system.Id : null);
        if (settings == null)
            throw Errors.MemberGuildNotFound;

        var patch = MemberGuildPatch.FromJson(data);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        var newSettings = await _repo.UpdateMemberGuild(member.Id, guild_id, patch);
        return Ok(newSettings.ToJson());
    }

    [HttpGet("messages/{messageId}")]
    public async Task<ActionResult<JObject>> MessageGet(ulong messageId)
    {
        var messageByOriginal = await _redis.GetOriginalMid(messageId);

        var msg = await _repo.GetFullMessage(messageByOriginal ?? messageId);
        if (msg == null)
            throw Errors.MessageNotFound;

        var ctx = msg.System == null ? LookupContext.ByNonOwner : ContextFor(msg.System);
        return msg.ToJson(ctx);
    }
}