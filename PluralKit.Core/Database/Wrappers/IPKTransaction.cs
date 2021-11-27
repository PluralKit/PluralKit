using System.Data;

namespace PluralKit.Core;

public interface IPKTransaction: IDbTransaction, IAsyncDisposable
{
    public Task CommitAsync(CancellationToken ct = default);
    public Task RollbackAsync(CancellationToken ct = default);
}