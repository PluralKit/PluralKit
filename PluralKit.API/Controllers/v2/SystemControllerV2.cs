using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using PluralKit.Core;

namespace PluralKit.API;

[ApiController]
[Route("v2/systems")]
public class SystemControllerV2: PKControllerBase
{
    public SystemControllerV2(IServiceProvider svc) : base(svc) { }

    [HttpGet("{systemRef}")]
    public async Task<IActionResult> SystemGet(string systemRef)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null) throw Errors.SystemNotFound;
        return Ok(system.ToJson(ContextFor(system)));
    }

    [HttpGet("{systemRef}/oembed.json")]
    public async Task<IActionResult> SystemEmbed(string systemRef)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null)
            throw Errors.SystemNotFound;

        return Ok(APIJsonExt.EmbedJson(system.NameFor(ContextFor(system)) ?? $"System with ID `{system.Hid}`", "System"));
    }

    [HttpPatch("{systemRef}")]
    public async Task<IActionResult> DoSystemPatch(string systemRef, [FromBody] JObject data)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null) throw Errors.SystemNotFound;
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;
        var patch = SystemPatch.FromJSON(data);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        var newSystem = await _repo.UpdateSystem(system.Id, patch);
        return Ok(newSystem.ToJson(LookupContext.ByOwner));
    }

    [HttpGet("{systemRef}/settings")]
    public async Task<IActionResult> GetSystemSettings(string systemRef)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null) throw Errors.SystemNotFound;
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        var config = await _repo.GetSystemConfig(system.Id);
        return Ok(config.ToJson());
    }

    [HttpPatch("{systemRef}/settings")]
    public async Task<IActionResult> DoSystemSettingsPatch(string systemRef, [FromBody] JObject data)
    {
        var system = await ResolveSystem(systemRef);
        if (system == null) throw Errors.SystemNotFound;
        if (ContextFor(system) != LookupContext.ByOwner)
            throw Errors.GenericMissingPermissions;

        var patch = SystemConfigPatch.FromJson(data);

        patch.AssertIsValid();
        if (patch.Errors.Count > 0)
            throw new ModelParseError(patch.Errors);

        var newConfig = await _repo.UpdateSystemConfig(system.Id, patch);
        return Ok(newConfig.ToJson());
    }
}