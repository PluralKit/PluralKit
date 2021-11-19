using System.Threading.Tasks;

using SqlKata;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public Task<GuildConfig> GetGuild(ulong guild)
        {
            var query = new Query("servers").AsInsert(new { id = guild });
            // sqlkata doesn't support postgres on conflict, so we just hack it on here
            return _db.QueryFirst<GuildConfig>(query, "on conflict (id) do update set id = @$1 returning *");
        }

        public Task UpdateGuild(ulong guild, GuildPatch patch)
        {
            _logger.Information("Updated guild {GuildId}: {@GuildPatch}", guild, patch);
            var query = patch.Apply(new Query("servers").Where("id", guild));
            return _db.ExecuteQuery(query, extraSql: "returning *");
        }


        public Task<SystemGuildSettings> GetSystemGuild(ulong guild, SystemId system, bool defaultInsert = true)
        {
            if (!defaultInsert)
                return _db.QueryFirst<SystemGuildSettings>(new Query("system_guild")
                    .Where("guild", guild)
                    .Where("system", system)
                );

            var query = new Query("system_guild").AsInsert(new
            {
                guild = guild,
                system = system
            });
            return _db.QueryFirst<SystemGuildSettings>(query,
                extraSql: "on conflict (guild, system) do update set guild = $1, system = $2 returning *"
            );
        }

        public async Task<SystemGuildSettings> UpdateSystemGuild(SystemId system, ulong guild, SystemGuildPatch patch)
        {
            _logger.Information("Updated {SystemId} in guild {GuildId}: {@SystemGuildPatch}", system, guild, patch);
            var query = patch.Apply(new Query("system_guild").Where("system", system).Where("guild", guild));
            var settings = await _db.QueryFirst<SystemGuildSettings>(query, extraSql: "returning *");
            _ = _dispatch.Dispatch(system, guild, patch);
            return settings;
        }

        public Task<MemberGuildSettings> GetMemberGuild(ulong guild, MemberId member, bool defaultInsert = true)
        {
            if (!defaultInsert)
                return _db.QueryFirst<MemberGuildSettings>(new Query("member_guild")
                    .Where("guild", guild)
                    .Where("member", member)
                );

            var query = new Query("member_guild").AsInsert(new
            {
                guild = guild,
                member = member
            });
            return _db.QueryFirst<MemberGuildSettings>(query,
                extraSql: "on conflict (guild, member) do update set guild = $1, member = $2 returning *"
            );
        }

        public Task<MemberGuildSettings> UpdateMemberGuild(MemberId member, ulong guild, MemberGuildPatch patch)
        {
            _logger.Information("Updated {MemberId} in guild {GuildId}: {@MemberGuildPatch}", member, guild, patch);
            var query = patch.Apply(new Query("member_guild").Where("member", member).Where("guild", guild));
            _ = _dispatch.Dispatch(member, guild, patch);
            return _db.QueryFirst<MemberGuildSettings>(query, extraSql: "returning *");
        }
    }
}