using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace PluralKit.Core
{
    public interface IPKCommand: IDbCommand, IAsyncDisposable
    {
        public Task PrepareAsync(CancellationToken ct = default);
        public Task<int> ExecuteNonQueryAsync(CancellationToken ct = default);
        public Task<object> ExecuteScalarAsync(CancellationToken ct = default);
        public Task<DbDataReader> ExecuteReaderAsync(CancellationToken ct = default);
        public Task<DbDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken ct = default);
    }
}