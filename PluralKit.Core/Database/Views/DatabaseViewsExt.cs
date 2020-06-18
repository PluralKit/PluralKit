#nullable enable
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public static class DatabaseViewsExt
    {
        public static Task<IEnumerable<SystemFronter>> QueryCurrentFronters(this IPKConnection conn, SystemId system) =>
            conn.QueryAsync<SystemFronter>("select * from system_fronters where system = @system", new {system});

        public static Task<IEnumerable<ListedMember>> QueryMemberList(this IPKConnection conn, SystemId system, LookupContext ctx, PrivacyLevel? privacyFilter = null, string? filter = null, bool includeDescriptionInNameFilter = false)
        {
            StringBuilder query = new StringBuilder("select * from member_list where system = @system");

            if (privacyFilter != null)
                query.Append($" and member_visibility = {(int) privacyFilter}");

            if (filter != null)
            {
                static string Filter(string column) => $"(position(lower(@filter) in lower(coalesce({column}, ''))) > 0)"; 

                query.Append($" and ({Filter("name")} or {Filter("display_name")}");
                if (includeDescriptionInNameFilter)
                {
                    // We need to account for the possibility of description privacy when searching
                    // If we're looking up from the outside, only search "public_description" (defined in the view; null if desc is private)
                    // If we're the owner, just search the full description
                    var descriptionColumn = ctx == LookupContext.ByOwner ? "description" : "public_description";
                    query.Append($"or {Filter(descriptionColumn)}");
                }
                query.Append(")");
            }
            
            return conn.QueryAsync<ListedMember>(query.ToString(), new {system, filter});
        }
    }
}