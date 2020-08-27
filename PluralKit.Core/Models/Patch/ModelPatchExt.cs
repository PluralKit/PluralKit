using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dapper;

using Serilog;

namespace PluralKit.Core
{
    public static class ModelPatchExt
    {
        public static Task<PKSystem> UpdateSystem(this IPKConnection conn, SystemId id, SystemPatch patch)
        {
            Log.ForContext("Elastic", "yes?").Information("Updated {SystemId}: {@SystemPatch}", id, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Update("systems", "id = @id"))
                .WithConstant("id", id)
                .Build("returning *");
            return conn.QueryFirstAsync<PKSystem>(query, pms);
        }

        public static Task DeleteSystem(this IPKConnection conn, SystemId id)
        {
            Log.ForContext("Elastic", "yes?").Information("Deleted {SystemId}", id);
            return conn.ExecuteAsync("delete from systems where id = @Id", new {Id = id});
        }

        public static async Task<PKMember> CreateMember(this IPKConnection conn, SystemId system, string memberName)
        {
            var member = await conn.QueryFirstAsync<PKMember>(
                "insert into members (hid, system, name) values (find_free_member_hid(), @SystemId, @Name) returning *",
                new {SystemId = system, Name = memberName});
            Log.ForContext("Elastic", "yes?").Information("Created {MemberId} in {SystemId}: {MemberName}", 
                system, member.Id, memberName);
            return member;
        }

        public static Task<PKMember> UpdateMember(this IPKConnection conn, MemberId id, MemberPatch patch)
        {
            Log.ForContext("Elastic", "yes?").Information("Updated {MemberId}: {@MemberPatch}", id, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Update("members", "id = @id"))
                .WithConstant("id", id)
                .Build("returning *");
            return conn.QueryFirstAsync<PKMember>(query, pms);
        }
        
        public static Task DeleteMember(this IPKConnection conn, MemberId id)
        {
            Log.ForContext("Elastic", "yes?").Information("Deleted {MemberId}", id);
            return conn.ExecuteAsync("delete from members where id = @Id", new {Id = id});
        }

        public static Task UpsertSystemGuild(this IPKConnection conn, SystemId system, ulong guild,
                                             SystemGuildPatch patch)
        {
            Log.ForContext("Elastic", "yes?").Information("Updated {SystemId} in guild {GuildId}: {@SystemGuildPatch}",  system, guild, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Upsert("system_guild", "system, guild"))
                .WithConstant("system", system)
                .WithConstant("guild", guild)
                .Build();
            return conn.ExecuteAsync(query, pms);
        }
        
        public static Task UpsertMemberGuild(this IPKConnection conn, MemberId member, ulong guild,
                                             MemberGuildPatch patch)
        {
            Log.ForContext("Elastic", "yes?").Information("Updated {MemberId} in guild {GuildId}: {@MemberGuildPatch}",  member, guild, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Upsert("member_guild", "member, guild"))
                .WithConstant("member", member)
                .WithConstant("guild", guild)
                .Build();
            return conn.ExecuteAsync(query, pms);
        }
        
        public static Task UpsertGuild(this IPKConnection conn, ulong guild, GuildPatch patch)
        {
            Log.ForContext("Elastic", "yes?").Information("Updated guild {GuildId}: {@GuildPatch}",  guild, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Upsert("servers", "id"))
                .WithConstant("id", guild)
                .Build();
            return conn.ExecuteAsync(query, pms);
        }
        
        public static async Task<PKGroup> CreateGroup(this IPKConnection conn, SystemId system, string name)
        {
            var group = await conn.QueryFirstAsync<PKGroup>(
                "insert into groups (hid, system, name) values (find_free_group_hid(), @System, @Name) returning *",
                new {System = system, Name = name});
            Log.ForContext("Elastic", "yes?").Information("Created group {GroupId} in system {SystemId}: {GroupName}", group.Id, system, name);
            return group;
        }

        public static Task<PKGroup> UpdateGroup(this IPKConnection conn, GroupId id, GroupPatch patch)
        {
            Log.ForContext("Elastic", "yes?").Information("Updated {GroupId}: {@GroupPatch}", id, patch);
            var (query, pms) = patch.Apply(UpdateQueryBuilder.Update("groups", "id = @id"))
                .WithConstant("id", id)
                .Build("returning *");
            return conn.QueryFirstAsync<PKGroup>(query, pms);
        }
        
        public static Task DeleteGroup(this IPKConnection conn, GroupId group)
        {
            Log.ForContext("Elastic", "yes?").Information("Deleted {GroupId}", group);
            return conn.ExecuteAsync("delete from groups where id = @Id", new {Id = @group});
        }

        public static async Task AddMembersToGroup(this IPKConnection conn, GroupId group, IReadOnlyCollection<MemberId> members)
        {
            await using var w = conn.BeginBinaryImport("copy group_members (group_id, member_id) from stdin (format binary)");
            foreach (var member in members)
            {
                await w.StartRowAsync();
                await w.WriteAsync(group.Value);
                await w.WriteAsync(member.Value);
            }
            await w.CompleteAsync();
            Log.ForContext("Elastic", "yes?").Information("Added members to {GroupId}: {MemberIds}", group, members);
        }
        
        public static Task RemoveMembersFromGroup(this IPKConnection conn, GroupId group, IReadOnlyCollection<MemberId> members)
        {
            Log.ForContext("Elastic", "yes?").Information("Removed members from {GroupId}: {MemberIds}", group, members);
            return conn.ExecuteAsync("delete from group_members where group_id = @Group and member_id = any(@Members)",
                new {Group = @group, Members = members.ToArray()});
        }
    }
}