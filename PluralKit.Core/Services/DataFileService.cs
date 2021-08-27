using System;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using Newtonsoft.Json.Linq;

using NodaTime;

using Serilog;

namespace PluralKit.Core
{
    public class DataFileService
    {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;
        private readonly ILogger _logger;

        public DataFileService(IDatabase db, ModelRepository repo, ILogger logger)
        {
            _db = db;
            _repo = repo;
            _logger = logger;
        }

        public async Task<JObject> ExportSystem(PKSystem system)
        {
            await using var conn = await _db.Obtain();

            var o = new JObject();

            o.Add("version", 1);
            o.Add("id", system.Hid);
            o.Add("name", system.Name);
            o.Add("description", system.Description);
            o.Add("tag", system.Tag);
            o.Add("avatar_url", system.AvatarUrl);
            o.Add("timezone", system.UiTz);
            o.Add("created", system.Created.FormatExport());
            o.Add("accounts", new JArray((await _repo.GetSystemAccounts(conn, system.Id)).ToList()));
            o.Add("members", new JArray((await _repo.GetSystemMembers(conn, system.Id).ToListAsync()).Select(m => m.ToJson(LookupContext.ByOwner))));

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

        public async Task<ImportResultNew> ImportSystem(ulong userId, PKSystem? system, JObject importFile, Func<string, Task> confirmFunc)
        {
            await using var conn = await _db.Obtain();
            await using var tx = await conn.BeginTransactionAsync();

            return await BulkImporter.PerformImport(conn, tx, _repo, _logger, userId, system, importFile, confirmFunc);
        }
    }
}