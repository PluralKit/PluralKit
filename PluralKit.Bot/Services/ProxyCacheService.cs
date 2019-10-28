using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Serilog;

namespace PluralKit.Bot
{
    public class ProxyCacheService
    {
        public class ProxyDatabaseResult
        {
            public PKSystem System;
            public PKMember Member;
        }
        
        private DbConnectionFactory _conn;
        private IMemoryCache _cache;
        private ILogger _logger;

        public ProxyCacheService(DbConnectionFactory conn, IMemoryCache cache, ILogger logger)
        {
            _conn = conn;
            _cache = cache;
            _logger = logger;
        }

        public Task<IEnumerable<ProxyDatabaseResult>> GetResultsFor(ulong account)
        {
            _logger.Verbose("Looking up members for account {Account} in cache...", account);
            return _cache.GetOrCreateAsync(GetKey(account), (entry) => FetchResults(account, entry));
        }

        public void InvalidateResultsFor(ulong account)
        {
            _logger.Information("Invalidating proxy cache for account {Account}", account);
            _cache.Remove(GetKey(account));
        }
        
        public async Task InvalidateResultsForSystem(PKSystem system)
        {
            _logger.Information("Invalidating proxy cache for system {System}", system.Id);
            using (var conn = await _conn.Obtain())
                foreach (var accountId in await conn.QueryAsync<ulong>("select uid from accounts where system = @Id", system))
                    _cache.Remove(GetKey(accountId));
        }

        private async Task<IEnumerable<ProxyDatabaseResult>> FetchResults(ulong account, ICacheEntry entry)
        {
            _logger.Information("Members for account {Account} not in cache, fetching", account);
            using (var conn = await _conn.Obtain())
            {
                var results = (await conn.QueryAsync<PKMember, PKSystem, ProxyDatabaseResult>(
                    "select members.*, systems.* from members, systems, accounts where members.system = systems.id and accounts.system = systems.id and accounts.uid = @Uid",
                    (member, system) =>
                        new ProxyDatabaseResult {Member = member, System = system}, new {Uid = account})).ToList();

                if (results.Count == 0)
                {
                    // Long expiry for accounts with no system registered
                    entry.SetSlidingExpiration(TimeSpan.FromMinutes(5));
                    entry.SetAbsoluteExpiration(TimeSpan.FromHours(1));
                }
                else
                {
                    // Shorter expiry if they already have a system
                    entry.SetSlidingExpiration(TimeSpan.FromMinutes(1));
                    entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
                }
                
                return results;
            }
        }

        private object GetKey(ulong account)
        {
            return $"_proxy_account_{account}";
        }
    }
}