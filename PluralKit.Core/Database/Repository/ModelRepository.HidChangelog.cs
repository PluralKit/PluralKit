using Dapper;

using SqlKata;

using NodaTime;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task<HidChangelog?> GetHidChangelogById(int id)
    {
        var query = new Query("hid_changelog").Where("id", id);
        return _db.QueryFirst<HidChangelog?>(query);
    }

    public async Task<HidChangelog> CreateHidChangelog(SystemId system, ulong discord_uid, string hid_type, string hid_old, string hid_new, IPKConnection? conn = null)
    {
        var query = new Query("hid_changelog").AsInsert(new { system, discord_uid, hid_type, hid_old, hid_new, });
        var changelog = await _db.QueryFirst<HidChangelog>(conn, query, "returning *");
        _logger.Information("Created HidChangelog {HidChangelogId} for system {SystemId}: {HidType} {OldHid} -> {NewHid}", changelog.Id, system, hid_type, hid_old, hid_new);
        return changelog;
    }

    public Task<int> GetHidChangelogCountForDate(SystemId system, LocalDate date)
    {
        var query = new Query("hid_changelog")
            .SelectRaw("count(*)")
            .Where("system", system)
            .WhereDate("created", date);

        return _db.QueryFirst<int>(query);
    }
}