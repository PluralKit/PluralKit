using SqlKata;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task<Counts> GetStats() => _db.QueryFirst<Counts>(new Query("info"));

    public class Counts
    {
        public int SystemCount { get; }
        public int MemberCount { get; }
        public int GroupCount { get; }
        public int SwitchCount { get; }
        public int MessageCount { get; }
    }
}