#nullable enable

using Dapper;

using SqlKata;
using SqlKata.Compilers;

namespace PluralKit.Core;

public static class DatabaseViewsExt
{
    public static async Task<IEnumerable<ListedGroup>> QueryGroupList(this IPKConnection conn, SystemId system,
                                                                  MemberId? memberFilter, bool withMembers = false)
    {
        PostgresCompiler compiler = new();

        Query query;
        if (memberFilter == null)
            query = new Query("group_list").Where("system", system.Value);
        else
            query = new Query("group_list")
                .From("group_members").Join("group_list", "group_list.id", "group_members.group_id")
                .Where("member_id", memberFilter);

        var q = compiler.Compile(query);
        var groups = await conn.QueryAsync<ListedGroup>(q.Sql, q.NamedBindings);

        if (!withMembers) return groups;

        var memberQuery = compiler.Compile(new Query("members").Where("system", system));
        var members = await conn.QueryAsync<PKMember>(memberQuery.Sql, memberQuery.NamedBindings);

        return groups.Select(g =>
        {
            g.Members = g.MemberIds?.Select(mId => members.First(m => m.Id.Value == mId)) ?? [];
            return g;
        });
    }
    public static async Task<IEnumerable<ListedMember>> QueryMemberList(this IPKConnection conn, SystemId system,
                                                                  GroupId? groupFilter, bool withGroups = false)
    {
        PostgresCompiler compiler = new();

        Query query;
        if (groupFilter == null)
            query = new Query("member_list").Where("system", system.Value);
        else
            query = new Query("member_list")
                .From("group_members").Join("member_list", "member_list.member_id", "group_members.member_id")
                .Where("group_id", groupFilter);

        var q = compiler.Compile(query);
        var members = await conn.QueryAsync<ListedMember>(q.Sql, q.NamedBindings);

        if (!withGroups) return members;

        var groupQuery = compiler.Compile(new Query("groups").Where("system", system));
        var groups = await conn.QueryAsync<PKGroup>(groupQuery.Sql, groupQuery.NamedBindings);

        return members.Select(m =>
        {
            m.Groups = m.GroupIds?.Select(gId => groups.First(g => g.Id.Value == gId)) ?? [];
            return m;
        });
    }
}