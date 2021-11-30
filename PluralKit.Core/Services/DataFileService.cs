using Newtonsoft.Json.Linq;

using NodaTime;

using Serilog;

namespace PluralKit.Core;

public class DataFileService
{
    private readonly IDatabase _db;
    private readonly DispatchService _dispatch;
    private readonly ILogger _logger;
    private readonly ModelRepository _repo;

    public DataFileService(IDatabase db, ModelRepository repo, ILogger logger, DispatchService dispatch)
    {
        _db = db;
        _repo = repo;
        _logger = logger;
        _dispatch = dispatch;
    }

    public async Task<JObject> ExportSystem(PKSystem system)
    {
        await using var conn = await _db.Obtain();

        var o = new JObject();
        o.Add("version", 1);

        o.Merge(system.ToJson(LookupContext.ByOwner));

        var config = await _repo.GetSystemConfig(system.Id);
        o.Add("config", config.ToJson());

        o.Add("accounts", new JArray((await _repo.GetSystemAccounts(system.Id)).ToList()));
        o.Add("members",
            new JArray((await _repo.GetSystemMembers(system.Id).ToListAsync()).Select(m =>
                m.ToJson(LookupContext.ByOwner))));

        var groups = await _repo.GetSystemGroups(system.Id).ToListAsync();
        var j_groups = groups.Select(x => x.ToJson(LookupContext.ByOwner, needsMembersArray: true)).ToList();

        if (groups.Count > 0)
        {
            var q = await _repo.GetGroupMemberInfo(groups.Select(x => x.Id));

            foreach (var row in q)
                ((JArray)j_groups.Find(x => x.Value<string>("id") == row.Group)["members"]).Add(row.Member);
        }

        o.Add("groups", new JArray(j_groups));

        var switches = new JArray();
        var switchList = await _repo.GetPeriodFronters(conn, system.Id, null,
            Instant.FromDateTimeUtc(DateTime.MinValue.ToUniversalTime()), SystemClock.Instance.GetCurrentInstant());
        foreach (var sw in switchList)
        {
            var s = new JObject();
            s.Add("timestamp", sw.TimespanStart.FormatExport());
            s.Add("members", new JArray(sw.Members.Select(m => m.Hid)));
            switches.Add(s);
        }

        o.Add("switches", switches);

        return o;
    }

    public async Task<ImportResultNew> ImportSystem(ulong userId, PKSystem? system, JObject importFile,
                                                    Func<string, Task> confirmFunc)
    {
        await using var conn = await _db.Obtain();
        await using var tx = await conn.BeginTransactionAsync();

        return await BulkImporter.PerformImport(conn, tx, _repo, _logger, _dispatch, userId, system, importFile,
            confirmFunc);
    }
}