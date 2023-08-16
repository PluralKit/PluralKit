#nullable enable
using System.Text;

using Dapper;

namespace PluralKit.Core;

public static class DatabaseViewsExt
{
    public static Task<IEnumerable<ListedGroup>> QueryGroupList(this IPKConnection conn, SystemId system,
                                                                  ListQueryOptions opts)
    {
        StringBuilder query = new StringBuilder("select * from group_list where system = @system");

        if (opts.PrivacyFilter != null && opts.PrivacyFilter != 0)
        {
            var numBits = (int)Math.Log2((int)opts.PrivacyFilter) + 1;
            query.Append($" and visibility::bit({numBits}) & {(int)opts.PrivacyFilter}::bit({numBits}) > 0::bit({numBits})");
        }

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
                var descriptionColumn = opts.Context switch
                {
                    LookupContext.ByOwner => "description",
                    LookupContext.ByTrusted => "trusted_description",
                    LookupContext.ByNonOwner => "public_description",
                    _ => "public_description"
                };
                query.Append($"or {Filter(descriptionColumn)}");
            }

            query.Append(")");
        }

        return conn.QueryAsync<ListedGroup>(
            query.ToString(),
            new { system, filter = opts.Search });
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

        if (opts.PrivacyFilter != 0)
        {
            var numBits = (int)Math.Log2((int)opts.PrivacyFilter) + 1;
            query.Append($" and member_visibility::bit({numBits}) & {(int)opts.PrivacyFilter}::bit({numBits}) > 0::bit({numBits})");
        }

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
                var descriptionColumn = opts.Context switch
                {
                    LookupContext.ByOwner => "description",
                    LookupContext.ByTrusted => "trusted_description",
                    LookupContext.ByNonOwner => "public_description",
                    _ => "public_description"
                };
                query.Append($"or {Filter(descriptionColumn)}");
            }

            query.Append(")");
        }

        return conn.QueryAsync<ListedMember>(query.ToString(),
            new { system, filter = opts.Search, groupFilter = opts.GroupFilter });
    }

    public struct ListQueryOptions
    {
        public PrivacyFilter PrivacyFilter;
        public string? Search;
        public bool SearchDescription;
        public LookupContext Context;
        public GroupId? GroupFilter;
    }
}