using SqlKata;

namespace PluralKit.Core;

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
        return _db.ExecuteQuery(query, "returning *");
    }


    public async Task<SystemGuildSettings> GetSystemGuild(ulong guild, SystemId system, bool defaultInsert = true, bool search = false)
    {
        if (!defaultInsert)
        {
            var simpleRes = await _db.QueryFirst<SystemGuildSettings>(new Query("system_guild")
                .Where("guild", guild)
                .Where("system", system)
            );
            if (simpleRes != null || !search)
                return simpleRes;

            var accounts = await GetSystemAccounts(system);

            var searchRes = await _db.QueryFirst<bool>(
                "select exists(select 1 from command_messages where guild = @guild and sender = any(@accounts))",
                new { guild = guild, accounts = accounts.Select(u => (long)u).ToArray() },
                queryName: "find_system_from_commands",
                messages: true
            );

            if (!searchRes)
                searchRes = await _db.QueryFirst<bool>(
                    "select exists(select 1 from command_messages where guild = @guild and sender = any(@accounts))",
                    new { guild = guild, accounts = accounts.Select(u => (long)u).ToArray() },
                    queryName: "find_system_from_messages",
                    messages: true
                );

            if (!searchRes)
                return null;
        }

        var query = new Query("system_guild").AsInsert(new { guild, system });
        return await _db.QueryFirst<SystemGuildSettings>(query,
            "on conflict (guild, system) do update set guild = $1, system = $2 returning *"
        );
    }

    public async Task<SystemGuildSettings> UpdateSystemGuild(SystemId system, ulong guild, SystemGuildPatch patch)
    {
        _logger.Information("Updated {SystemId} in guild {GuildId}: {@SystemGuildPatch}", system, guild, patch);
        var query = patch.Apply(new Query("system_guild").Where("system", system).Where("guild", guild));
        var settings = await _db.QueryFirst<SystemGuildSettings>(query, "returning *");
        _ = _dispatch.Dispatch(system, guild, patch);
        return settings;
    }

    public async Task<MemberGuildSettings> GetMemberGuild(ulong guild, MemberId member, bool defaultInsert = true, SystemId? search = null)
    {
        if (!defaultInsert)
        {
            var simpleRes = await _db.QueryFirst<MemberGuildSettings>(new Query("member_guild")
                .Where("guild", guild)
                .Where("member", member)
            );
            if (simpleRes != null || !search.HasValue)
                return simpleRes;

            var systemConfig = await GetSystemGuild(guild, search.Value, defaultInsert: false, search: true);

            if (systemConfig == null)
                return null;
        }

        var query = new Query("member_guild").AsInsert(new { guild, member });
        return await _db.QueryFirst<MemberGuildSettings>(query,
            "on conflict (guild, member) do update set guild = $1, member = $2 returning *"
        );
    }

    public Task<MemberGuildSettings> UpdateMemberGuild(MemberId member, ulong guild, MemberGuildPatch patch)
    {
        _logger.Information("Updated {MemberId} in guild {GuildId}: {@MemberGuildPatch}", member, guild, patch);
        var query = patch.Apply(new Query("member_guild").Where("member", member).Where("guild", guild));
        _ = _dispatch.Dispatch(member, guild, patch);
        return _db.QueryFirst<MemberGuildSettings>(query, "returning *");
    }
}