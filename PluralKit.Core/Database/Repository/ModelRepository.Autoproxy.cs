using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public async Task<AutoproxySettings> UpsertAutoproxySettings(IPKConnection conn, SystemId system, ulong? location, AutoproxyScope scope, AutoproxyPatch patch)
        {
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Upsert("autoproxy", "system, location"))
                .WithConstant("system", system)
                .WithConstant("scope", scope)
                // postgres really doesn't like primary keys to be null, so we use zero for global scope
                // it doesn't really matter since we check scope before location
                .WithConstant("location", location ?? 0)
                .Build("returning *");
            return await conn.QueryFirstAsync<AutoproxySettings>(query, pms);
        }

        public async Task<AutoproxySettings> GetAutoproxySettings(IPKConnection conn, SystemId system, AutoproxyScope scope, ulong? location) =>
            await conn.QueryFirstOrDefaultAsync<AutoproxySettings>("select * from autoproxy where system = @System and scope = @Scope and location = @Location", new { System = system, Scope = scope, Location = location ?? 0});

        public async Task ClearAutoproxySettings(IPKConnection conn, SystemId system, AutoproxyScope scope, ulong? location) =>
            await conn.QueryAsync("delete from autoproxy where system = @System and scope = @Scope and location = @Location", new { System = system, Scope = scope, Location = location});
    }
}