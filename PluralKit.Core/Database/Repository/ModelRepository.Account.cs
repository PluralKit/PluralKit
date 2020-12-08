using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public async Task UpdateAccount(IPKConnection conn, ulong id, AccountPatch patch)
        {
            _logger.Information("Updated account {accountId}: {@AccountPatch}", id, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Update("accounts", "uid = @uid"))
                .WithConstant("uid", id)
                .Build();
            await conn.ExecuteAsync(query, pms);
        }

    }
}