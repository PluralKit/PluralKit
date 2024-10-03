using SqlKata;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public Task<Counts> GetStats() => _db.QueryFirst<Counts>(new Query("info"));

    public class Counts
    {
        public long SystemCount { get; }
        public long MemberCount { get; }
        public long GroupCount { get; }
        public long SwitchCount { get; }
        public long MessageCount { get; }
    }
}