#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public Task<PKSystem?> GetSystem(IPKConnection conn, SystemId id) =>
            conn.QueryFirstOrDefaultAsync<PKSystem?>("select * from systems where id = @id", new {id});

        public Task<PKSystem?> GetSystemByGuid(IPKConnection conn, Guid id) =>
            conn.QueryFirstOrDefaultAsync<PKSystem?>("select * from systems where uuid = @id", new {id});

        public Task<PKSystem?> GetSystemByAccount(IPKConnection conn, ulong accountId) =>
            conn.QuerySingleOrDefaultAsync<PKSystem?>(
                "select systems.* from systems, accounts where accounts.system = systems.id and accounts.uid = @Id",
                new {Id = accountId});

        public Task<PKSystem?> GetSystemByHid(IPKConnection conn, string hid) =>
            conn.QuerySingleOrDefaultAsync<PKSystem?>("select * from systems where systems.hid = @Hid",
                new {Hid = hid.ToLower()});

        public Task<IEnumerable<ulong>> GetSystemAccounts(IPKConnection conn, SystemId system) =>
            conn.QueryAsync<ulong>("select uid from accounts where system = @Id", new {Id = system});

        public IAsyncEnumerable<PKMember> GetSystemMembers(IPKConnection conn, SystemId system) =>
            conn.QueryStreamAsync<PKMember>("select * from members where system = @SystemID", new {SystemID = system});

        public Task<int> GetSystemMemberCount(IPKConnection conn, SystemId id, PrivacyLevel? privacyFilter = null)
        {
            var query = new StringBuilder("select count(*) from members where system = @Id");
            if (privacyFilter != null)
                query.Append($" and member_visibility = {(int) privacyFilter.Value}");
            return conn.QuerySingleAsync<int>(query.ToString(), new {Id = id});
        }

        public async Task<PKSystem> CreateSystem(IPKConnection conn, string? systemName = null)
        {
            var system = await conn.QuerySingleAsync<PKSystem>(
                "insert into systems (hid, name) values (find_free_system_hid(), @Name) returning *",
                new {Name = systemName});
            _logger.Information("Created {SystemId}", system.Id);
            return system;
        }

        public Task<PKSystem> UpdateSystem(IPKConnection conn, SystemId id, SystemPatch patch)
        {
            _logger.Information("Updated {SystemId}: {@SystemPatch}", id, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Update("systems", "id = @id"))
                .WithConstant("id", id)
                .Build("returning *");
            return conn.QueryFirstAsync<PKSystem>(query, pms);
        }

        public async Task AddAccount(IPKConnection conn, SystemId system, ulong accountId)
        {
            // We have "on conflict do nothing" since linking an account when it's already linked to the same system is idempotent
            // This is used in import/export, although the pk;link command checks for this case beforehand
            await conn.ExecuteAsync("insert into accounts (uid, system) values (@Id, @SystemId) on conflict do nothing",
                new {Id = accountId, SystemId = system});
            _logger.Information("Linked account {UserId} to {SystemId}", accountId, system);
        }

        public async Task RemoveAccount(IPKConnection conn, SystemId system, ulong accountId)
        {
            await conn.ExecuteAsync("delete from accounts where uid = @Id and system = @SystemId",
                new {Id = accountId, SystemId = system});
            _logger.Information("Unlinked account {UserId} from {SystemId}", accountId, system);
        }

        public Task DeleteSystem(IPKConnection conn, SystemId id)
        {
            _logger.Information("Deleted {SystemId}", id);
            return conn.ExecuteAsync("delete from systems where id = @Id", new {Id = id});
        }
    }
}