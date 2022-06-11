using Dapper;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public async Task UpdateStats()
    {
        await _db.Execute(conn =>
            conn.ExecuteAsync("update info set system_count = (select count(*) from systems)"));
        await _db.Execute(conn =>
            conn.ExecuteAsync("update info set member_count = (select count(*) from members)"));
        await _db.Execute(conn =>
            conn.ExecuteAsync("update info set group_count = (select count(*) from groups)"));
        await _db.Execute(conn =>
            conn.ExecuteAsync("update info set switch_count = (select count(*) from switches)"));
        await _db.Execute(conn =>
            conn.ExecuteAsync("update info set message_count = (select count(*) from messages)"));
    }

    public Task<Counts> GetStats()
        => _db.Execute(conn => conn.QuerySingleAsync<Counts>("select * from info"));

    public class Counts
    {
        public int SystemCount { get; }
        public int MemberCount { get; }
        public int GroupCount { get; }
        public int SwitchCount { get; }
        public int MessageCount { get; }
    }
}