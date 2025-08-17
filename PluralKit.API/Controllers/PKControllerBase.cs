using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc;

using PluralKit.Core;

namespace PluralKit.API;

public class PKControllerBase: ControllerBase
{
    private readonly Guid _requestId = Guid.NewGuid();
    private readonly Regex _snowflakeRegex = new("^[0-9]{17,19}$");

    private List<PKMember>? _memberLookupCache { get; set; }
    private List<PKGroup>? _groupLookupCache { get; set; }

    protected readonly ApiConfig _config;
    protected readonly IDatabase _db;
    protected readonly ModelRepository _repo;
    protected readonly RedisService _redis;
    protected readonly DispatchService _dispatch;

    public PKControllerBase(IServiceProvider svc)
    {
        _config = svc.GetRequiredService<ApiConfig>();
        _db = svc.GetRequiredService<IDatabase>();
        _repo = svc.GetRequiredService<ModelRepository>();
        _redis = svc.GetRequiredService<RedisService>();
        _dispatch = svc.GetRequiredService<DispatchService>();
    }

    protected Task<PKSystem?> ResolveSystem(string systemRef)
    {
        if (systemRef == "@me")
        {
            HttpContext.Items.TryGetValue("SystemId", out var systemId);
            if (systemId == null)
                throw Errors.GenericAuthError;
            return _repo.GetSystem((SystemId)systemId);
        }

        if (Guid.TryParse(systemRef, out var guid))
            return _repo.GetSystemByGuid(guid);

        if (_snowflakeRegex.IsMatch(systemRef))
            return _repo.GetSystemByAccount(ulong.Parse(systemRef));

        if (systemRef.TryParseHid(out var hid))
            return _repo.GetSystemByHid(hid);

        return Task.FromResult<PKSystem?>(null);
    }

    protected async Task<PKMember?> ResolveMember(string memberRef, bool cache = false)
    {
        if (cache)
        {
            if (_memberLookupCache == null)
            {
                HttpContext.Items.TryGetValue("SystemId", out var systemId);
                if (systemId == null)
                    throw new Exception("Authenticated user must not be null to use lookup cache!");

                _memberLookupCache = await _repo.GetSystemMembers((SystemId)systemId).ToListAsync();
            }

            return _memberLookupCache.FirstOrDefault(x => x.Hid == memberRef || x.Uuid.ToString() == memberRef);
        }

        if (Guid.TryParse(memberRef, out var guid))
            return await _repo.GetMemberByGuid(guid);

        if (memberRef.TryParseHid(out var hid))
            return await _repo.GetMemberByHid(hid);

        return null;
    }

    protected async Task<PKGroup?> ResolveGroup(string groupRef, bool cache = false)
    {
        if (cache)
        {
            if (_groupLookupCache == null)
            {
                HttpContext.Items.TryGetValue("SystemId", out var systemId);
                if (systemId == null)
                    throw new Exception("Authenticated user must not be null to use lookup cache!");

                _groupLookupCache = await _repo.GetSystemGroups((SystemId)systemId).ToListAsync();
            }

            return _groupLookupCache.FirstOrDefault(x => x.Hid == groupRef || x.Uuid.ToString() == groupRef);
        }

        if (Guid.TryParse(groupRef, out var guid))
            return await _repo.GetGroupByGuid(guid);

        if (groupRef.TryParseHid(out var hid))
            return await _repo.GetGroupByHid(hid);

        return null;
    }

    protected bool IsAuthenticatedAs(SystemId system)
    {
        HttpContext.Items.TryGetValue("SystemId", out var systemId);
        return systemId != null && (SystemId)systemId == system;
    }

    protected LookupContext ContextFor(PKSystem system)
    {
        HttpContext.Items.TryGetValue("SystemId", out var systemId);
        HttpContext.Items.TryGetValue("LookupContext", out var lookupCtx);
        if (systemId != null && (SystemId)systemId == system.Id)
        {
            if (lookupCtx != null) return (LookupContext)lookupCtx;
            return LookupContext.ByOwner;
        }

        return LookupContext.ByNonOwner;
    }

    protected LookupContext ContextFor(PKMember member)
    {
        HttpContext.Items.TryGetValue("SystemId", out var systemId);
        HttpContext.Items.TryGetValue("LookupContext", out var lookupCtx);
        if (systemId != null && (SystemId)systemId == member.System)
        {
            if (lookupCtx != null) return (LookupContext)lookupCtx;
            return LookupContext.ByOwner;
        }

        return LookupContext.ByNonOwner;
    }

    protected LookupContext ContextFor(PKGroup group)
    {
        HttpContext.Items.TryGetValue("SystemId", out var systemId);
        HttpContext.Items.TryGetValue("LookupContext", out var lookupCtx);
        if (systemId != null && (SystemId)systemId == group.System)
        {
            if (lookupCtx != null) return (LookupContext)lookupCtx;
            return LookupContext.ByOwner;
        }

        return LookupContext.ByNonOwner;
    }
}