using Dapper;

using SqlKata;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task<AbuseLog?> GetAbuseLogByGuid(Guid id)
    {
        var query = new Query("abuse_logs").Where("uuid", id);
        return _db.QueryFirst<AbuseLog?>(query);
    }

    public Task<AbuseLog?> GetAbuseLogByAccount(ulong accountId)
    {
        var query = new Query("accounts")
            .Select("abuse_logs.*")
            .LeftJoin("abuse_logs", "abuse_logs.id", "accounts.abuse_log")
            .Where("uid", accountId)
            .WhereNotNull("abuse_log");

        return _db.QueryFirst<AbuseLog?>(query);
    }

    public Task<IEnumerable<ulong>> GetAbuseLogAccounts(AbuseLogId id)
    {
        var query = new Query("accounts").Select("uid").Where("abuse_log", id);
        return _db.Query<ulong>(query);
    }

    public async Task<AbuseLog> CreateAbuseLog(string? desc = null, bool? denyBotUsage = null, IPKConnection? conn = null)
    {
        var query = new Query("abuse_logs").AsInsert(new
        {
            description = desc,
            deny_bot_usage = denyBotUsage,
        });

        var abuseLog = await _db.QueryFirst<AbuseLog>(conn, query, "returning *");
        _logger.Information("Created {AbuseLogId}", abuseLog.Id);
        return abuseLog;
    }

    public async Task AddAbuseLogAccount(AbuseLogId abuseLog, ulong accountId, IPKConnection? conn = null)
    {
        var query = new Query("accounts").AsInsert(new
        {
            abuse_log = abuseLog,
            uid = accountId,
        });
        await _db.ExecuteQuery(conn, query, "on conflict (uid) do update set abuse_log = @p0");

        _logger.Information("Linked account {UserId} to {AbuseLogId}", accountId, abuseLog);
    }

    public async Task<AbuseLog> UpdateAbuseLog(AbuseLogId id, AbuseLogPatch patch, IPKConnection? conn = null)
    {
        _logger.Information("Updated {AbuseLogId}: {@AbuseLogPatch}", id, patch);
        var query = patch.Apply(new Query("abuse_logs").Where("id", id));
        return await _db.QueryFirst<AbuseLog>(conn, query, "returning *");
    }

    public async Task DeleteAbuseLog(AbuseLogId id)
    {
        var query = new Query("abuse_logs").AsDelete().Where("id", id);
        await _db.ExecuteQuery(query);
        _logger.Information("Deleted {AbuseLogId}", id);
    }
}