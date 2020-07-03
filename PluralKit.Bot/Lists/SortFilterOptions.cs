using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NodaTime;

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
            var str = new StringBuilder();
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
        
        public async Task<IEnumerable<ListedMember>> Execute(IPKConnection conn, PKSystem system, LookupContext ctx)
        {
            var filtered = await QueryWithFilter(conn, system, ctx);
            return Sort(filtered, ctx);
        }

        private Task<IEnumerable<ListedMember>> QueryWithFilter(IPKConnection conn, PKSystem system, LookupContext ctx) =>
            conn.QueryMemberList(system.Id, ctx, PrivacyFilter switch
            {
                PrivacyFilter.PrivateOnly => PrivacyLevel.Private,
                PrivacyFilter.PublicOnly => PrivacyLevel.Public,
                PrivacyFilter.All => null,
                _ => throw new ArgumentOutOfRangeException($"Unknown privacy filter {PrivacyFilter}")
            }, Filter, SearchInDescription);

        private IEnumerable<ListedMember> Sort(IEnumerable<ListedMember> input, LookupContext ctx)
        {
            IComparer<T> ReverseMaybe<T>(IComparer<T> c) =>
                Reverse ? Comparer<T>.Create((a, b) => c.Compare(b, a)) : c;
            
            var culture = StringComparer.InvariantCultureIgnoreCase;
            return (SortProperty switch
            {
                // As for the OrderByDescending HasValue calls: https://www.jerriepelser.com/blog/orderby-with-null-values/
                // We want nulls last no matter what, even if orders are reversed
                SortProperty.Hid => input.OrderBy(m => m.Hid, ReverseMaybe(culture)),
                SortProperty.Name => input.OrderBy(m => m.NameFor(ctx), ReverseMaybe(culture)),
                SortProperty.CreationDate => input.OrderBy(m => m.Created, ReverseMaybe(Comparer<Instant>.Default)),
                SortProperty.MessageCount => input.OrderByDescending(m => m.MessageCount, ReverseMaybe(Comparer<int>.Default)),
                SortProperty.DisplayName => input
                    .OrderByDescending(m => m.DisplayName != null)
                    .ThenBy(m => m.DisplayName, ReverseMaybe(culture)),
                SortProperty.Birthdate => input
                    .OrderByDescending(m => m.AnnualBirthday.HasValue)
                    .ThenBy(m => m.AnnualBirthday, ReverseMaybe(Comparer<AnnualDate?>.Default)),
                SortProperty.LastMessage => input
                    .OrderByDescending(m => m.LastMessage.HasValue)
                    .ThenByDescending(m => m.LastMessage, ReverseMaybe(Comparer<ulong?>.Default)),
                SortProperty.LastSwitch => input
                    .OrderByDescending(m => m.LastSwitchTime.HasValue)
                    .ThenByDescending(m => m.LastSwitchTime, ReverseMaybe(Comparer<Instant?>.Default)),
                _ => throw new ArgumentOutOfRangeException($"Unknown sort property {SortProperty}")
            })
                // Lastly, add a by-name fallback order for collisions (generally hits w/ lots of null values)
                .ThenBy(m => m.NameFor(ctx), culture);
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
            if (ctx.MatchFlag("search-description", "filter-description", "in-description", "sd", "description", "desc"))
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

    public enum PrivacyFilter
    {
        All,
        PublicOnly,
        PrivateOnly
    }
}