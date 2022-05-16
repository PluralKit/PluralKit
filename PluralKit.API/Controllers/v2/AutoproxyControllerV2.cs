using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

[ApiController]
[Route("v2")]
public class AutoproxyControllerV2: PKControllerBase
{
    public AutoproxyControllerV2(IServiceProvider svc) : base(svc) { }

    [HttpGet("systems/{systemRef}/autoproxy")]
    [HttpPatch("systems/{systemRef}/autoproxy")]
    public async Task<IActionResult> Entrypoint(string systemRef, [FromQuery] ulong? guild_id, [FromQuery] ulong? channel_id, [FromBody] JObject? data)
    {
        var system = await ResolveSystem(systemRef);
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        if (HttpContext.Request.Method == "GET")
            return await Get(system, guild_id, channel_id);
        else if (HttpContext.Request.Method == "GET")
            return StatusCode(501);
        // compiler pls
        else
            // should never get here
            throw new ArgumentOutOfRangeException();
    }

    public async Task<IActionResult> Get(PKSystem system, ulong? guildId, ulong? channelId)
    {
        var settings = _repo.GetAutoproxySettings(system.Id, guildId, channelId);

        if (settings == null)
            return NotFound();

        return Ok(settings);
    }
}