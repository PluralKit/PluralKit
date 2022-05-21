using System.Data;
using System.Data.Common;

using Npgsql;

namespace PluralKit.Core;

internal class PKTransaction: DbTransaction, IPKTransaction
{
    public PKTransaction(NpgsqlTransaction inner)
    {
        Inner = inner;
    }

    public NpgsqlTransaction Inner { get; }

    protected override DbConnection DbConnection => Inner.Connection;

    public override void Commit() => throw SyncError(nameof(Commit));
    public override Task CommitAsync(CancellationToken ct = default) => Inner.CommitAsync(ct);

    public override void Rollback() => throw SyncError(nameof(Rollback));
    public override Task RollbackAsync(CancellationToken ct = default) => Inner.RollbackAsync(ct);
    public override IsolationLevel IsolationLevel => Inner.IsolationLevel;

    private static Exception SyncError(string caller) =>
        throw new Exception($"Executed synchronous IDbTransaction function {caller}!");
}