using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

[ApiController]
[ApiVersion("2.0")]
[Route("v{version:apiVersion}")]
public class DiscordControllerV2: PKControllerBase
{
    public DiscordControllerV2(IServiceProvider svc) : base(svc) { }


    [HttpGet("systems/{systemRef}/guilds/{guild_id}")]
    public async Task<IActionResult> SystemGuildGet(string systemRef, ulong guild_id)
    {
        var system = await ResolveSystem(systemRef);
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        var settings = await _repo.GetSystemGuild(guild_id, system.Id, false);
        if (settings == null)
            throw Errors.SystemGuildNotFound;

        PKMember member = null;
        if (settings.AutoproxyMember != null)
            member = await _repo.GetMember(settings.AutoproxyMember.Value);

        return Ok(settings.ToJson(member?.Uuid.ToString()));
    }

    [HttpPatch("systems/{systemRef}/guilds/{guild_id}")]
    public async Task<IActionResult> DoSystemGuildPatch(string systemRef, ulong guild_id, [FromBody] JObject data)
    {
        var system = await ResolveSystem(systemRef);
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        var settings = await _repo.GetSystemGuild(guild_id, system.Id, false);
        if (settings == null)
            throw Errors.SystemGuildNotFound;

        MemberId? memberId = null;
        if (data.ContainsKey("autoproxy_member"))
        {
            if (data["autoproxy_member"].Type != JTokenType.Null)
            {
                var member = await ResolveMember(data.Value<string>("autoproxy_member"));
                if (member == null)
                    throw Errors.MemberNotFound;

                memberId = member.Id;
            }
        }
        else
        {
            memberId = settings.AutoproxyMember;
        }

        var patch = SystemGuildPatch.FromJson(data, memberId);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        // this is less than great, but at least it's legible
        if (patch.AutoproxyMember.Value == null)
        {
            if (patch.AutoproxyMode.IsPresent)
            {
                if (patch.AutoproxyMode.Value == AutoproxyMode.Member)
                    throw Errors.MissingAutoproxyMember;
            }
            else if (settings.AutoproxyMode == AutoproxyMode.Member)
            {
                throw Errors.MissingAutoproxyMember;
            }
        }
        else
        {
            if (patch.AutoproxyMode.IsPresent)
            {
                if (patch.AutoproxyMode.Value == AutoproxyMode.Latch)
                    throw Errors.PatchLatchMemberError;
            }
            else if (settings.AutoproxyMode == AutoproxyMode.Latch)
            {
                throw Errors.PatchLatchMemberError;
            }
        }

        var newSettings = await _repo.UpdateSystemGuild(system.Id, guild_id, patch);

        PKMember? newMember = null;
        if (newSettings.AutoproxyMember != null)
            newMember = await _repo.GetMember(newSettings.AutoproxyMember.Value);
        return Ok(newSettings.ToJson(newMember?.Hid));
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

        var settings = await _repo.GetMemberGuild(guild_id, member.Id, false);
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

        var settings = await _repo.GetMemberGuild(guild_id, member.Id, false);
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
        var msg = await _db.Execute(c => _repo.GetMessage(c, messageId));
        if (msg == null)
            throw Errors.MessageNotFound;

        var ctx = ContextFor(msg.System);
        return msg.ToJson(ctx, APIVersion.V2);
    }
}