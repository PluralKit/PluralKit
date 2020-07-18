#nullable enable
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public static class ModelQueryExt
    {
        public static Task<PKSystem?> QuerySystem(this IPKConnection conn, SystemId id) =>
            conn.QueryFirstOrDefaultAsync<PKSystem?>("select * from systems where id = @id", new {id});

        public static Task<int> GetSystemMemberCount(this IPKConnection conn, SystemId id, PrivacyLevel? privacyFilter = null)
        {
            var query = new StringBuilder("select count(*) from members where system = @Id");
            if (privacyFilter != null)
                query.Append($" and member_visibility = {(int) privacyFilter.Value}");
            return conn.QuerySingleAsync<int>(query.ToString(), new {Id = id});
        }

        public static Task<IEnumerable<ulong>> GetLinkedAccounts(this IPKConnection conn, SystemId id) =>
            conn.QueryAsync<ulong>("select uid from accounts where system = @Id", new {Id = id});

        public static Task<PKMember?> QueryMember(this IPKConnection conn, MemberId id) =>
            conn.QueryFirstOrDefaultAsync<PKMember?>("select * from members where id = @id", new {id});
        
        public static Task<PKMember?> QueryMemberByHid(this IPKConnection conn, string hid) =>
            conn.QueryFirstOrDefaultAsync<PKMember?>("select * from members where hid = @hid", new {hid = hid.ToLowerInvariant()});
        
        public static Task<PKGroup?> QueryGroupByName(this IPKConnection conn, string name) =>
            conn.QueryFirstOrDefaultAsync<PKGroup?>("select * from groups where lower(name) = lower(@name)", new {name = name});
        
        public static Task<PKGroup?> QueryGroupByHid(this IPKConnection conn, string hid) =>
            conn.QueryFirstOrDefaultAsync<PKGroup?>("select * from groups where hid = @hid", new {hid = hid.ToLowerInvariant()});

        public static Task<IEnumerable<PKGroup>> QueryGroupsInSystem(this IPKConnection conn, SystemId system) =>
            conn.QueryAsync<PKGroup>("select * from groups where system = @System", new {System = system});

        public static Task<int> QueryGroupMemberCount(this IPKConnection conn, GroupId id,
                                                      PrivacyLevel? privacyFilter = null)
        {
            var query = new StringBuilder("select count(*) from group_members");
            if (privacyFilter != null)
                query.Append(" left join members on group_members.member_id = members.id");
            query.Append(" where group_members.group_id = @Id");
            if (privacyFilter != null)
                query.Append(" and members.member_visibility = @PrivacyFilter");
            return conn.QuerySingleOrDefaultAsync<int>(query.ToString(), new {Id = id, PrivacyFilter = privacyFilter});
        }

        public static Task<GuildConfig> QueryOrInsertGuildConfig(this IPKConnection conn, ulong guild) =>
            conn.QueryFirstAsync<GuildConfig>("insert into servers (id) values (@guild) on conflict (id) do update set id = @guild returning *", new {guild});

        public static Task<SystemGuildSettings> QueryOrInsertSystemGuildConfig(this IPKConnection conn, ulong guild, SystemId system) =>
            conn.QueryFirstAsync<SystemGuildSettings>(
                "insert into system_guild (guild, system) values (@guild, @system) on conflict (guild, system) do update set guild = @guild, system = @system returning *", 
                new {guild, system});

        public static Task<MemberGuildSettings> QueryOrInsertMemberGuildConfig(
            this IPKConnection conn, ulong guild, MemberId member) =>
            conn.QueryFirstAsync<MemberGuildSettings>(
                "insert into member_guild (guild, member) values (@guild, @member) on conflict (guild, member) do update set guild = @guild, member = @member returning *",
                new {guild, member});
    }
}