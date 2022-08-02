using Dapper;

namespace PluralKit.Core;

public partial class ModelRepository
{
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