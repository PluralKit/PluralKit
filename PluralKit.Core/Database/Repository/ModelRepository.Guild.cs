using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
    {
        public Task UpsertGuild(IPKConnection conn, ulong guild, GuildPatch patch)
        {
            _logger.Information("Updated guild {GuildId}: {@GuildPatch}", guild, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Upsert("servers", "id"))
                .WithConstant("id", guild)
                .Build();
            return conn.ExecuteAsync(query, pms);
        }
        
        public Task UpsertSystemGuild(IPKConnection conn, SystemId system, ulong guild,
                                      SystemGuildPatch patch)
        {
            _logger.Information("Updated {SystemId} in guild {GuildId}: {@SystemGuildPatch}", system, guild, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Upsert("system_guild", "system, guild"))
                .WithConstant("system", system)
                .WithConstant("guild", guild)
                .Build();
            return conn.ExecuteAsync(query, pms);
        }

        public Task UpsertMemberGuild(IPKConnection conn, MemberId member, ulong guild,
                                      MemberGuildPatch patch)
        {
            _logger.Information("Updated {MemberId} in guild {GuildId}: {@MemberGuildPatch}", member, guild, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Upsert("member_guild", "member, guild"))
                .WithConstant("member", member)
                .WithConstant("guild", guild)
                .Build();
            return conn.ExecuteAsync(query, pms);
        }
        
        public Task<GuildConfig> GetGuild(IPKConnection conn, ulong guild) =>
            conn.QueryFirstAsync<GuildConfig>("insert into servers (id) values (@guild) on conflict (id) do update set id = @guild returning *", new {guild});

        public Task<SystemGuildSettings> GetSystemGuild(IPKConnection conn, ulong guild, SystemId system) =>
            conn.QueryFirstAsync<SystemGuildSettings>(
                "insert into system_guild (guild, system) values (@guild, @system) on conflict (guild, system) do update set guild = @guild, system = @system returning *", 
                new {guild, system});

        public Task<MemberGuildSettings> GetMemberGuild(IPKConnection conn, ulong guild, MemberId member) =>
            conn.QueryFirstAsync<MemberGuildSettings>(
                "insert into member_guild (guild, member) values (@guild, @member) on conflict (guild, member) do update set guild = @guild, member = @member returning *",
                new {guild, member});
    }
}