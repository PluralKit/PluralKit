using System;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using Microsoft.Extensions.Caching.Memory;

namespace PluralKit.Bot
{
    public class AutoproxyCacheResult
    {
        public SystemGuildSettings GuildSettings;
        public PKSystem System;
        public PKMember AutoproxyMember;
    }
    public class AutoproxyCacheService
    {
        private IMemoryCache _cache;
        private IDataStore _data;
        private DbConnectionFactory _conn;

        public AutoproxyCacheService(IMemoryCache cache, DbConnectionFactory conn, IDataStore data)
        {
            _cache = cache;
            _conn = conn;
            _data = data;
        }

        public async Task<AutoproxyCacheResult> GetGuildSettings(ulong account, ulong guild) => 
            await _cache.GetOrCreateAsync(GetKey(account, guild), entry => FetchSettings(account, guild, entry));

        public async Task FlushCacheForSystem(PKSystem system, ulong guild)
        {
            foreach (var account in await _data.GetSystemAccounts(system))
                FlushCacheFor(account, guild);
        }

        public void FlushCacheFor(ulong account, ulong guild) => 
            _cache.Remove(GetKey(account, guild));

        private async Task<AutoproxyCacheResult> FetchSettings(ulong account, ulong guild, ICacheEntry entry)
        {
            using var conn = await _conn.Obtain();
            var data = (await conn.QueryAsync<SystemGuildSettings, PKSystem, PKMember, AutoproxyCacheResult>(
                "select system_guild.*, systems.*, members.* from accounts inner join systems on systems.id = accounts.system inner join system_guild on system_guild.system = systems.id left join members on system_guild.autoproxy_member = members.id where accounts.uid = @Uid and system_guild.guild = @Guild",
                (guildSettings, system, autoproxyMember) => new AutoproxyCacheResult
                {
                    GuildSettings = guildSettings,
                    System = system,
                    AutoproxyMember = autoproxyMember
                },
                new {Uid = account, Guild = guild})).FirstOrDefault();
            
            if (data != null)
            {
                // Long expiry for accounts with no system/settings registered
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(5));
                entry.SetAbsoluteExpiration(TimeSpan.FromHours(1));
            }
            else
            {
                // Shorter expiry if they already have settings
                entry.SetSlidingExpiration(TimeSpan.FromMinutes(1));
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            }
            
            return data;
        }

        private string GetKey(ulong account, ulong guild) => $"_system_guild_{account}_{guild}";
    }
}