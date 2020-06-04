using System.Text;

using PluralKit.Core;

namespace PluralKit.Bot
{
    public class SortFilterOptions
    {
        public SortProperty SortProperty = SortProperty.Name;
        public SortDirection Direction = SortDirection.Ascending;
        public PrivacyFilter PrivacyFilter = PrivacyFilter.PublicOnly;
        public string Filter = null;
        public bool SearchInDescription = false;

        public string CreateFilterString()
        {
            // TODO
            return "uwu";
        }

        public string BuildQuery()
        {
            // Select clause
            StringBuilder query = new StringBuilder();
            query.Append(SortProperty switch
            {
                SortProperty.MessageCount =>
                "select members.*, count(messages.*) as message_count, max(messages.mid) as last_message from members",
                SortProperty.LastMessage =>
                "select members.*, count(messages.*) as message_count, max(messages.mid) as last_message from members",
                SortProperty.LastSwitch => "select members.*, max(switches.timestamp) as last_switch_time from members",
                _ => "select members.* from members"
            });

            // Join clauses
            query.Append(SortProperty switch
            {
                SortProperty.MessageCount => " left join messages on messages.member = members.id",
                SortProperty.LastMessage => " left join messages on messages.member = members.id",
                SortProperty.LastSwitch =>
                " left join switch_members on switch_members.member = members.id left join switches on switch_members.switch = switches.id",
                _ => ""
            });

            // Where clauses
            query.Append(" where members.system = @System");

            // Privacy filter
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

            // Group clause
            query.Append(SortProperty switch
            {
                SortProperty.MessageCount => " group by members.id",
                SortProperty.LastMessage => " group by members.id",
                SortProperty.LastSwitch => " group by members.id",
                _ => ""
            });

            // Order clause
            query.Append(SortProperty switch
            {
                SortProperty.Hid => " order by members.hid",
                SortProperty.CreationDate => " order by members.created",
                SortProperty.Birthdate =>
                " order by extract(month from members.birthday), extract(day from members.birthday)",
                SortProperty.MessageCount => " order by count(messages.mid)",
                SortProperty.LastMessage => " order by max(messages.mid)",
                SortProperty.LastSwitch => " order by max(switches.timestamp)",
                _ => " order by members.name"
            });

            // Order direction
            if (Direction == SortDirection.Descending)
                query.Append(" desc");

            return query.ToString();
        }

        public static SortFilterOptions FromFlags(Context ctx)
        {
            var p = new SortFilterOptions();
            
            // Direction
            if (ctx.MatchFlag("r", "rev", "reverse", "desc", "descending"))
                p.Direction = SortDirection.Descending;
            
            // Sort
            if (ctx.MatchFlag("by-id", "bi"))
                p.SortProperty = SortProperty.Hid;
            else if (ctx.MatchFlag("by-msgcount", "by-mc", "by-msgs", "bm"))
                p.SortProperty = SortProperty.MessageCount;
            else if (ctx.MatchFlag("by-date", "by-time", "by-created", "bc"))
                p.SortProperty = SortProperty.CreationDate;
            else if (ctx.MatchFlag("by-switch"))
                p.SortProperty = SortProperty.LastSwitch;
            else if (ctx.MatchFlag("by-last-message"))
                p.SortProperty = SortProperty.LastMessage;
            
            // Description
            if (ctx.MatchFlag("desc"))
                p.SearchInDescription = true;
            
            // Privacy
            if (ctx.MatchFlag("a", "all"))
                p.PrivacyFilter = PrivacyFilter.All;
            else if (ctx.MatchFlag("po", "private", "private-only", "only-private", "priv"))
                p.PrivacyFilter = PrivacyFilter.PrivateOnly;
            
            return p;
        }
    }

    public enum SortProperty
    {
        Name,
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