using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using Microsoft.Extensions.Caching.Memory;

using Serilog;

namespace PluralKit.Core
{
    public class ProxyCache
    {
        // We can NOT depend on IDataStore as that creates a cycle, since it needs access to call the invalidation methods
        private IMemoryCache _cache;
        private DbConnectionFactory _db;
        private ILogger _logger;

        public ProxyCache(IMemoryCache cache, DbConnectionFactory db, ILogger logger)
        {
            _cache = cache;
            _db = db;
            _logger = logger;
        }

        public Task InvalidateSystem(PKSystem system) => InvalidateSystem(system.Id);

        public void InvalidateDeletedSystem(int systemId, IEnumerable<ulong> accounts)
        {
            // Used when the system's already removed so we can't look up accounts
            // We assume the account list is saved already somewhere and can be passed here (which is the case in Store)
            
            _cache.Remove(KeyForSystem(systemId));
            foreach (var account in accounts) 
                _cache.Remove(KeyForAccount(account));
        }

        public async Task InvalidateSystem(int systemId)
        {
            if (_cache.TryGetValue<CachedAccount>(KeyForSystem(systemId), out var systemCache))
            {
                // If we have the system cached here, just invalidate for all the accounts we have in the cache
                _logger.Debug("Invalidating cache for system {System} and accounts {Accounts}", systemId, systemCache.Accounts);
                _cache.Remove(KeyForSystem(systemId));
                foreach (var account in systemCache.Accounts) 
                    _cache.Remove(KeyForAccount(account));
                return;
            }
            
            // If we don't, look up the accounts from the database and invalidate *those*
            
            _cache.Remove(KeyForSystem(systemId));
            using var conn = await _db.Obtain();
            var accounts = (await conn.QueryAsync<ulong>("select uid from accounts where system = @System", new {System = systemId})).ToArray();
            _logger.Debug("Invalidating cache for system {System} and accounts {Accounts}", systemId, accounts);
            foreach (var account in accounts)
                _cache.Remove(KeyForAccount(account));
        }

        public void InvalidateGuild(ulong guild)
        {
            _logger.Debug("Invalidating cache for guild {Guild}", guild);
            _cache.Remove(KeyForGuild(guild));
        }

        public async Task<GuildConfig> GetGuildDataCached(ulong guild)
        {
            if (_cache.TryGetValue<GuildConfig>(KeyForGuild(guild), out var item))
            {
                _logger.Verbose("Cache hit for guild {Guild}", guild);
                return item;
            }

            // When changing this, also see PostgresDataStore::GetOrCreateGuildConfig
            using var conn = await _db.Obtain();
            
            _logger.Verbose("Cache miss for guild {Guild}", guild);
            var guildConfig = (await conn.QuerySingleOrDefaultAsync<PostgresDataStore.DatabaseCompatibleGuildConfig>(
                "insert into servers (id) values (@Id) on conflict do nothing; select * from servers where id = @Id",
                new {Id = guild})).Into();
            
            _cache.CreateEntry(KeyForGuild(guild))
                .SetValue(guildConfig)
                .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                .SetAbsoluteExpiration(TimeSpan.FromMinutes(30))
                .Dispose(); // Don't ask, but this *saves* the entry. Somehow.
            return guildConfig;
        }

        public async Task<CachedAccount> GetAccountDataCached(ulong account)
        {
            if (_cache.TryGetValue<CachedAccount>(KeyForAccount(account), out var item))
            {
                _logger.Verbose("Cache hit for account {Account}", account);
                return item;
            }
            
            _logger.Verbose("Cache miss for account {Account}", account);

            var data = await GetAccountData(account);
            if (data == null)
            {
                _logger.Debug("Cached data for account {Account} (no system)", account);
                
                // If we didn't find any value, set a pretty long expiry and the value to null
                _cache.CreateEntry(KeyForAccount(account))
                    .SetValue(null)
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                    .SetAbsoluteExpiration(TimeSpan.FromHours(1))
                    .Dispose(); // Don't ask, but this *saves* the entry. Somehow.
                return null;
            }
            
            // If we *did* find the value, cache it for *every account in the system* with a shorter expiry
            _logger.Debug("Cached data for system {System} and accounts {Account}", data.System.Id, data.Accounts);
            foreach (var linkedAccount in data.Accounts)
            {
                _cache.CreateEntry(KeyForAccount(linkedAccount))
                    .SetValue(data)
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(20))
                    .Dispose(); // Don't ask, but this *saves* the entry. Somehow.

                // And also do it for the system itself so we can look up by that
                _cache.CreateEntry(KeyForSystem(data.System.Id))
                    .SetValue(data)
                    .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(20))
                    .Dispose(); // Don't ask, but this *saves* the entry. Somehow.
            }

            return data;
        }

        private async Task<CachedAccount> GetAccountData(ulong account)
        {
            using var conn = await _db.Obtain();
            
            // Doing this as two queries instead of a two-step join to avoid sending duplicate rows for the system over the network for each member
            // This *may* be less efficient, haven't done too much stuff about this but having the system ID saved is very useful later on
            
            var system = await conn.QuerySingleOrDefaultAsync<PKSystem>("select systems.* from accounts inner join systems on systems.id = accounts.system where accounts.uid = @Account", new { Account = account });
            if (system == null) return null; // No system = no members = no cache value

            // Fetches:
            // - List of accounts in the system
            // - List of members in the system
            // - List of guild settings for the system (for every guild)
            // - List of guild settings for each member (for every guild)
            // I'm slightly worried the volume of guild settings will get too much, but for simplicity reasons I decided
            // against caching them individually per-guild, since I can't imagine they'll be edited *that* much
            var result = await conn.QueryMultipleAsync(@"
                select uid from accounts where system = @System;
                select * from members where system = @System;
                select * from system_guild where system = @System;
                select member_guild.* from members inner join member_guild on member_guild.member = members.id where members.system = @System; 
            ", new {System = system.Id});
            
            return new CachedAccount
            {
                System = system, 
                Accounts = (await result.ReadAsync<ulong>()).ToArray(),
                Members = (await result.ReadAsync<PKMember>()).ToArray(),
                SystemGuild = (await result.ReadAsync<SystemGuildSettings>()).ToArray(),
                MemberGuild = (await result.ReadAsync<MemberGuildSettings>()).ToArray()
            };
        }

        private string KeyForAccount(ulong account) => $"_account_cache_{account}";
        private string KeyForSystem(int system) => $"_system_cache_{system}";
        private string KeyForGuild(ulong guild) => $"_guild_cache_{guild}";
    }

    public class CachedAccount
    {
        public PKSystem System;
        public PKMember[] Members;
        public SystemGuildSettings[] SystemGuild;
        public MemberGuildSettings[] MemberGuild;
        public ulong[] Accounts;

        public SystemGuildSettings SettingsForGuild(ulong guild) =>
            SystemGuild.FirstOrDefault(s => s.Guild == guild) ?? new SystemGuildSettings();
        
        public MemberGuildSettings SettingsForMemberGuild(int memberId, ulong guild) =>
            MemberGuild.FirstOrDefault(m => m.Member == memberId && m.Guild == guild) ?? new MemberGuildSettings();
    }
}