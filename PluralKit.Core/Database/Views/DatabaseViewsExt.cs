#nullable enable

using Dapper;

using SqlKata;
using SqlKata.Compilers;

namespace PluralKit.Core;

public static class DatabaseViewsExt
{
    public static async Task<IEnumerable<ListedGroup>> QueryGroupList(this IPKConnection conn, SystemId system,
                                                                  ListQueryOptions opts, bool withMembers = false)
    {
        PostgresCompiler compiler = new();

        Query query;
        if (opts.MemberFilter == null)
            query = new Query("group_list").Where("system", system.Value);
        else
            query = new Query("group_list")
                .From("group_members").Join("group_list", "group_list.id", "group_members.group_id")
                .Where("member_id", opts.MemberFilter);

        if (opts.PrivacyFilter != null)
            query.Where("visibility", (int)opts.PrivacyFilter);

        if (opts.Search != null)
        {
            string Filter(string column) =>
                $"(position(lower('{opts.Search}') in lower(coalesce({column}, ''))) > 0)";

            var nameColumn = opts.Context == LookupContext.ByOwner ? "name" : "public_name";
            query.Where(q =>
            {
                q.OrWhereRaw(Filter(nameColumn)).OrWhereRaw(Filter("display_name"));
                if (opts.SearchDescription)
                {
                    // We need to account for the possibility of description privacy when searching
                    // If we're looking up from the outside, only search "public_description" (defined in the view; null if desc is private)
                    // If we're the owner, just search the full description
                    var descriptionColumn =
                        opts.Context == LookupContext.ByOwner ? "description" : "public_description";
                    q.OrWhereRaw(Filter(descriptionColumn));
                }

                return q;
            });
        }

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
                                                                  ListQueryOptions opts, bool withGroups = false)
    {
        PostgresCompiler compiler = new();

        Query query;
        if (opts.GroupFilter == null)
            query = new Query("member_list").Where("system", system.Value);
        else
            query = new Query("member_list")
                .From("group_members").Join("member_list", "member_list.member_id", "group_members.member_id")
                .Where("group_id", opts.GroupFilter.Value);

        if (opts.PrivacyFilter != null)
            query.Where("member_visibility", (int)opts.PrivacyFilter);

        if (opts.Search != null)
        {
            string Filter(string column) =>
                $"(position(lower('{opts.Search}') in lower(coalesce({column}, ''))) > 0)";
            string AliasFilter(string col) =>
                $"(exists (select 1 from unnest({col}) as alias where position(lower('{opts.Search}') in lower(alias)) > 0))";

            var nameColumn = opts.Context == LookupContext.ByOwner ? "name" : "public_name";
            var aliasColumn = opts.Context == LookupContext.ByOwner ? "aliases" : "public_aliases";

            query.Where(q =>
            {
                q.OrWhereRaw(Filter(nameColumn)).OrWhereRaw(Filter("display_name")).OrWhereRaw(AliasFilter(aliasColumn));

                if (opts.SearchDescription)
                {
                    // We need to account for the possibility of description privacy when searching
                    // If we're looking up from the outside, only search "public_description" (defined in the view; null if desc is private)
                    // If we're the owner, just search the full description
                    var descriptionColumn =
                        opts.Context == LookupContext.ByOwner ? "description" : "public_description";
                    q.OrWhereRaw(Filter(descriptionColumn));
                }

                return q;
            });
        }

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

    public struct ListQueryOptions
    {
        public PrivacyLevel? PrivacyFilter;
        public string? Search;
        public bool SearchDescription;
        public LookupContext Context;
        public GroupId? GroupFilter;
        public MemberId? MemberFilter;
    }
}