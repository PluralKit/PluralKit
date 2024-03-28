#nullable enable
using System.Text;

using Dapper;

namespace PluralKit.Core;

public static class DatabaseViewsExt
{
    public static Task<IEnumerable<ListedGroup>> QueryGroupList(this IPKConnection conn, SystemId system,
                                                                  ListQueryOptions opts)
    {
        StringBuilder query;
        if (opts.MemberFilter == null)
            query = new StringBuilder("select * from group_list where system = @system");
        else
            query = new StringBuilder("select group_list.* from group_members inner join group_list on group_list.id = group_members.group_id where member_id = @MemberFilter");

        if (opts.PrivacyFilter != null)
            query.Append($" and visibility = {(int)opts.PrivacyFilter}");

        if (opts.Search != null)
        {
            static string Filter(string column) =>
                $"(position(lower(@filter) in lower(coalesce({column}, ''))) > 0)";

            query.Append($" and ({Filter("name")} or {Filter("display_name")}");
            if (opts.SearchDescription)
            {
                // We need to account for the possibility of description privacy when searching
                // If we're looking up from the outside, only search "public_description" (defined in the view; null if desc is private)
                // If we're the owner, just search the full description
                var descriptionColumn =
                    opts.Context == LookupContext.ByOwner ? "description" : "public_description";
                query.Append($"or {Filter(descriptionColumn)}");
            }

            query.Append(")");
        }

        return conn.QueryAsync<ListedGroup>(
            query.ToString(),
            new { system, filter = opts.Search, memberFilter = opts.MemberFilter });
    }
    public static Task<IEnumerable<ListedMember>> QueryMemberList(this IPKConnection conn, SystemId system,
                                                                  ListQueryOptions opts)
    {
        StringBuilder query;
        if (opts.GroupFilter == null)
            query = new StringBuilder("select * from member_list where system = @system");
        else
            query = new StringBuilder(
                "select member_list.* from group_members inner join member_list on member_list.id = group_members.member_id where group_id = @groupFilter");

        if (opts.PrivacyFilter != null)
            query.Append($" and member_visibility = {(int)opts.PrivacyFilter}");

        if (opts.Search != null)
        {
            static string Filter(string column) =>
                $"(position(lower(@filter) in lower(coalesce({column}, ''))) > 0)";

            query.Append($" and ({Filter("name")} or {Filter("display_name")}");
            if (opts.SearchDescription)
            {
                // We need to account for the possibility of description privacy when searching
                // If we're looking up from the outside, only search "public_description" (defined in the view; null if desc is private)
                // If we're the owner, just search the full description
                var descriptionColumn =
                    opts.Context == LookupContext.ByOwner ? "description" : "public_description";
                query.Append($"or {Filter(descriptionColumn)}");
            }

            query.Append(")");
        }

        return conn.QueryAsync<ListedMember>(query.ToString(),
            new { system, filter = opts.Search, groupFilter = opts.GroupFilter });
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