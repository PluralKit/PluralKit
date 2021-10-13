using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("v{version:apiVersion}")]
    public class GuildControllerV2: PKControllerBase
    {
        public GuildControllerV2(IServiceProvider svc) : base(svc) { }


        [HttpGet("systems/@me/guilds/{guild_id}")]
        public async Task<IActionResult> SystemGuildGet(ulong guild_id)
        {
            var system = await ResolveSystem("@me");
            var settings = await _repo.GetSystemGuild(guild_id, system.Id, defaultInsert: false);
            if (settings == null)
                throw APIErrors.SystemGuildNotFound;

            PKMember member = null;
            if (settings.AutoproxyMember != null)
                member = await _repo.GetMember(settings.AutoproxyMember.Value);

            return Ok(settings.ToJson(member?.Hid));
        }

        [HttpPatch("systems/@me/guilds/{guild_id}")]
        public async Task<IActionResult> DoSystemGuildPatch(ulong guild_id, [FromBody] JObject data)
        {
            var system = await ResolveSystem("@me");
            var settings = await _repo.GetSystemGuild(guild_id, system.Id, defaultInsert: false);
            if (settings == null)
                throw APIErrors.SystemGuildNotFound;

            MemberId? memberId = null;
            if (data.ContainsKey("autoproxy_member"))
            {
                if (data["autoproxy_member"].Type != JTokenType.Null)
                {
                    var member = await ResolveMember(data.Value<string>("autoproxy_member"));
                    if (member == null)
                        throw APIErrors.MemberNotFound;

                    memberId = member.Id;
                }
            }
            else
                memberId = settings.AutoproxyMember;

            var patch = SystemGuildPatch.FromJson(data, memberId);

            patch.AssertIsValid();
            if (patch.Errors.Count > 0)
                throw new ModelParseError(patch.Errors);

            // this is less than great, but at least it's legible
            if (patch.AutoproxyMember.Value == null)
                if (patch.AutoproxyMode.IsPresent)
                {
                    if (patch.AutoproxyMode.Value == AutoproxyMode.Member)
                        throw APIErrors.MissingAutoproxyMember;
                }
                else if (settings.AutoproxyMode == AutoproxyMode.Member)
                    throw APIErrors.MissingAutoproxyMember;

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
                throw APIErrors.MemberNotFound;
            if (member.System != system.Id)
                throw APIErrors.NotOwnMemberError;

            var settings = await _repo.GetMemberGuild(guild_id, member.Id, defaultInsert: false);
            if (settings == null)
                throw APIErrors.MemberGuildNotFound;

            return Ok(settings.ToJson());
        }

        [HttpPatch("members/{memberRef}/guilds/{guild_id}")]
        public async Task<IActionResult> DoMemberGuildPatch(string memberRef, ulong guild_id, [FromBody] JObject data)
        {
            var system = await ResolveSystem("@me");
            var member = await ResolveMember(memberRef);
            if (member == null)
                throw APIErrors.MemberNotFound;
            if (member.System != system.Id)
                throw APIErrors.NotOwnMemberError;

            var settings = await _repo.GetMemberGuild(guild_id, member.Id, defaultInsert: false);
            if (settings == null)
                throw APIErrors.MemberGuildNotFound;

            var patch = MemberGuildPatch.FromJson(data);

            patch.AssertIsValid();
            if (patch.Errors.Count > 0)
                throw new ModelParseError(patch.Errors);

            var newSettings = await _repo.UpdateMemberGuild(member.Id, guild_id, patch);
            return Ok(newSettings.ToJson());
        }


    }
}