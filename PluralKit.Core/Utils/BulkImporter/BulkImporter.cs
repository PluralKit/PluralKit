using Dapper;

using Newtonsoft.Json.Linq;

using Serilog;

namespace PluralKit.Core;

public partial class BulkImporter: IAsyncDisposable
{
    private readonly Dictionary<string, GroupId> _existingGroupHids = new();
    private readonly Dictionary<string, GroupId> _existingGroupNames = new();

    private readonly Dictionary<string, MemberId> _existingMemberHids = new();
    private readonly Dictionary<string, MemberId> _existingMemberNames = new();
    private readonly Dictionary<string, GroupId> _knownGroupIdentifiers = new();
    private readonly Dictionary<string, MemberId> _knownMemberIdentifiers = new();

    private readonly ImportResultNew _result = new();
    private ILogger _logger { get; init; }
    private ModelRepository _repo { get; init; }

    private PKSystem _system { get; set; }
    private SystemConfig _cfg { get; set; }
    private IPKConnection _conn { get; init; }
    private IPKTransaction _tx { get; init; }

    private Func<string, Task> _confirmFunc { get; init; }

    public async ValueTask DisposeAsync()
    {
        // try rolling back the transaction
        // this will throw if the transaction was committed, but that's fine
        // so we just catch InvalidOperationException
        try
        {
            await _tx.RollbackAsync();
        }
        catch (InvalidOperationException) { }
    }

    internal static async Task<ImportResultNew> PerformImport(IPKConnection conn, IPKTransaction tx,
                        ModelRepository repo, ILogger logger, DispatchService dispatch, ulong userId,
                        PKSystem? system, JObject importFile, Func<string, Task> confirmFunc)
    {
        await using var importer = new BulkImporter
        {
            _logger = logger,
            _repo = repo,
            _system = system,
            _conn = conn,
            _tx = tx,
            _confirmFunc = confirmFunc
        };

        if (system == null)
        {
            system = await repo.CreateSystem(null, importer._conn);
            await repo.AddAccount(system.Id, userId, importer._conn);
            importer._result.CreatedSystem = system.Hid;
            importer._system = system;
        }

        importer._cfg = await repo.GetSystemConfig(system.Id, conn);

        // Fetch all members in the system and log their names and hids
        var members = await conn.QueryAsync<PKMember>("select id, hid, name from members where system = @System",
            new { System = system.Id });
        foreach (var m in members)
        {
            importer._existingMemberHids[m.Hid] = m.Id;
            importer._existingMemberNames[m.Name] = m.Id;
        }

        // same as above for groups
        var groups = await conn.QueryAsync<PKGroup>("select id, hid, name from groups where system = @System",
            new { System = system.Id });
        foreach (var g in groups)
        {
            importer._existingGroupHids[g.Hid] = g.Id;
            importer._existingGroupNames[g.Name] = g.Id;
        }

        try
        {
            if (importFile.ContainsKey("tuppers"))
                await importer.ImportTupperbox(importFile);
            else if (importFile.ContainsKey("switches"))
                await importer.ImportPluralKit(importFile);
            else
                throw new ImportException("File type is unknown.");
            importer._result.Success = true;
            await tx.CommitAsync();

            _ = dispatch.Dispatch(system.Id, new UpdateDispatchData { Event = DispatchEvent.SUCCESSFUL_IMPORT });
        }
        catch (ImportException e)
        {
            importer._result.Success = false;
            importer._result.Message = e.Message;
        }
        catch (ArgumentNullException)
        {
            importer._result.Success = false;
        }

        return importer._result;
    }

    private (MemberId?, bool) TryGetExistingMember(string hid, string name)
    {
        if (_existingMemberHids.TryGetValue(hid, out var byHid)) return (byHid, true);
        if (_existingMemberNames.TryGetValue(name, out var byName)) return (byName, false);
        return (null, false);
    }

    private (GroupId?, bool) TryGetExistingGroup(string hid, string name)
    {
        if (_existingGroupHids.TryGetValue(hid, out var byHid)) return (byHid, true);
        if (_existingGroupNames.TryGetValue(name, out var byName)) return (byName, false);
        return (null, false);
    }

    private async Task AssertMemberLimitNotReached(int newMembers)
    {
        var memberLimit = _cfg.MemberLimitOverride ?? Limits.MaxMemberCount;
        var existingMembers = await _repo.GetSystemMemberCount(_system.Id);
        if (existingMembers + newMembers > memberLimit)
            throw new ImportException($"Import would exceed the maximum number of members ({memberLimit}).");
    }

    private async Task AssertGroupLimitNotReached(int newGroups)
    {
        var limit = _cfg.GroupLimitOverride ?? Limits.MaxGroupCount;
        var existing = await _repo.GetSystemGroupCount(_system.Id);
        if (existing + newGroups > limit)
            throw new ImportException($"Import would exceed the maximum number of groups ({limit}).");
    }

    private class ImportException: Exception
    {
        public ImportException(string Message) : base(Message) { }
    }
}

public record ImportResultNew
{
    public int Added = 0;
    public string? CreatedSystem;
    public string? Message;
    public int Modified = 0;
    public bool Success;
}