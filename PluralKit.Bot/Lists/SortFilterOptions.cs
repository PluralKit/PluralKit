using System;
using System.Text;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class SortFilterOptions
    {
        public SortProperty SortProperty = SortProperty.Name;
        public bool Reverse = false;
        public PrivacyFilter PrivacyFilter = PrivacyFilter.PublicOnly;
        public string Filter = null;
        public bool SearchInDescription = false;

        public string CreateFilterString()
        {
            StringBuilder str = new StringBuilder();
            str.Append("Sorting by ");
            str.Append(SortProperty switch
            {
                SortProperty.Name => "member name",
                SortProperty.Hid => "member ID",
                SortProperty.DisplayName => "display name",
                SortProperty.CreationDate => "creation date",
                SortProperty.LastMessage => "last message",
                SortProperty.LastSwitch => "last switch",
                SortProperty.MessageCount => "message count",
                SortProperty.Birthdate => "birthday",
                _ => new ArgumentOutOfRangeException($"Couldn't find readable string for sort property {SortProperty}")
            });
            
            if (Filter != null)
            {
                str.Append($", searching for \"{Filter}\"");
                if (SearchInDescription) str.Append(" (including description)");
            }

            str.Append(PrivacyFilter switch
            {
                PrivacyFilter.All => ", showing all members",
                PrivacyFilter.PrivateOnly => ", showing only private members",
                PrivacyFilter.PublicOnly => "", // (default, no extra line needed)
                _ => new ArgumentOutOfRangeException($"Couldn't find readable string for privacy filter {PrivacyFilter}")
            });
            
            return str.ToString();
        }

        public string BuildQuery()
        {
            // For best performance, add index:
            // - `on switch_members using btree (member asc nulls last) include (switch);`
            // TODO: add a migration adding this, perhaps lumped with the rest of the DB changes (it's there in prod)
            // TODO: also, this should be moved to a view, ideally
            
            // Select clause
            StringBuilder query = new StringBuilder();
            query.Append("select members.*, message_info.*");
            query.Append(", (select max(switches.timestamp) from switch_members inner join switches on switches.id = switch_members.switch where switch_members.member = members.id) as last_switch_time");
            query.Append(" from members");
            
            // Join here to enforce index scan on messages table by member, collect both max and count in one swoop
            query.Append(" left join lateral (select count(messages.mid) as message_count, max(messages.mid) as last_message from messages where messages.member = members.id) as message_info on true");

            // Filtering
            query.Append(" where members.system = @System");
            query.Append(PrivacyFilter switch
            {
                PrivacyFilter.PrivateOnly => $" and members.member_privacy = {(int) PrivacyLevel.Private}",
                PrivacyFilter.PublicOnly => $" and members.member_privacy = {(int) PrivacyLevel.Public}",
                _ => ""
            });

            // String filter
            if (Filter != null)
            {
                // Use position rather than ilike to not bother with escaping and such
                query.Append(" and (");
                query.Append(
                    "position(lower(@Filter) in lower(members.name)) > 0 or position(lower(@Filter) in lower(coalesce(members.display_name, ''))) > 0");
                if (SearchInDescription)
                    query.Append(" or position(lower(@Filter) in lower(coalesce(members.description, ''))) > 0");
                query.Append(")");
            }

            // Order clause
            query.Append(SortProperty switch
            {
                // Name/DN order needs `collate "C"` to match legacy .NET behavior (affects sorting of emojis, etc)
                SortProperty.Name => " order by members.name collate \"C\"",
                SortProperty.DisplayName => " order by members.display_name, members.name collate \"C\"",
                SortProperty.Hid => " order by members.hid",
                SortProperty.CreationDate => " order by members.created",
                SortProperty.Birthdate =>
                " order by extract(month from members.birthday), extract(day from members.birthday)",
                SortProperty.MessageCount => " order by message_count",
                SortProperty.LastMessage => " order by last_message",
                SortProperty.LastSwitch => " order by last_switch_time",
                _ => throw new ArgumentOutOfRangeException($"Couldn't find order clause for sort property {SortProperty}")
            });

            // Order direction
            var direction = SortProperty switch
            {
                // Some of these make more "logical sense" as descending (ie. "last message" = descending order of message timestamp/ID)
                SortProperty.MessageCount => SortDirection.Descending,
                SortProperty.LastMessage => SortDirection.Descending,
                SortProperty.LastSwitch => SortDirection.Descending,
                _ => SortDirection.Ascending
            };
            if (Reverse) direction = direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
            query.Append(direction == SortDirection.Ascending ? " asc" : " desc");
            query.Append(" nulls last");

            return query.ToString();
        }
        
        public static SortFilterOptions FromFlags(Context ctx)
        {
            var p = new SortFilterOptions();
            
            // Sort property (default is by name, but adding a flag anyway, 'cause why not)
            if (ctx.MatchFlag("by-name", "bn")) p.SortProperty = SortProperty.Name;
            if (ctx.MatchFlag("by-display-name", "bdn")) p.SortProperty = SortProperty.DisplayName;
            if (ctx.MatchFlag("by-id", "bid")) p.SortProperty = SortProperty.Hid;
            if (ctx.MatchFlag("by-message-count", "bmc")) p.SortProperty = SortProperty.MessageCount;
            if (ctx.MatchFlag("by-created", "bc")) p.SortProperty = SortProperty.CreationDate;
            if (ctx.MatchFlag("by-last-fronted", "by-last-front", "by-last-switch", "blf", "bls")) p.SortProperty = SortProperty.LastSwitch;
            if (ctx.MatchFlag("by-last-message", "blm", "blp")) p.SortProperty = SortProperty.LastMessage;
            if (ctx.MatchFlag("by-birthday", "by-birthdate", "bbd")) p.SortProperty = SortProperty.Birthdate;
            
            // Sort reverse
            if (ctx.MatchFlag("r", "rev", "reverse"))
                p.Reverse = true;

            // Include description in filter?
            if (ctx.MatchFlag("search-description", "filter-description", "in-description", "description", "desc"))
                p.SearchInDescription = true;
            
            // Privacy filter (default is public only)
            if (ctx.MatchFlag("a", "all")) p.PrivacyFilter = PrivacyFilter.All; 
            if (ctx.MatchFlag("private-only")) p.PrivacyFilter = PrivacyFilter.PrivateOnly;
            if (ctx.MatchFlag("public-only")) p.PrivacyFilter = PrivacyFilter.PublicOnly;
            
            return p;
        }
        
    }

    public enum SortProperty
    {
        Name,
        DisplayName,
        Hid,
        MessageCount,
        CreationDate,
        LastSwitch,
        LastMessage,
        Birthdate
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public enum PrivacyFilter
    {
        All,
        PublicOnly,
        PrivateOnly
    }
}