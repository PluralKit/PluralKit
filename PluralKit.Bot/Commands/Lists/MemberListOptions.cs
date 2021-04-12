using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NodaTime;

using PluralKit.Core;

#nullable enable
namespace PluralKit.Bot
{
    public class MemberListOptions
    {
        public SortProperty SortProperty { get; set; } = SortProperty.Name;
        public bool Reverse { get; set; }

        public PrivacyLevel? PrivacyFilter { get; set; } = PrivacyLevel.Public;
        public GroupId? GroupFilter { get; set; }
        public string? Search { get; set; }
        public bool SearchDescription { get; set; }
        
        public ListType Type { get; set; }
        public bool IncludeMessageCount { get; set; }
        public bool IncludeLastSwitch { get; set; }
        public bool IncludeLastMessage { get; set; }
        public bool IncludeCreated { get; set; }
        public bool IncludeAvatar { get; set; }
        public bool IncludePronouns { get; set; }
        
        public string CreateFilterString()
        {
            var str = new StringBuilder();
            str.Append("Sorting ");
            if (SortProperty != SortProperty.Random) str.Append("by ");
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
                SortProperty.Random => "randomly",
                _ => new ArgumentOutOfRangeException($"Couldn't find readable string for sort property {SortProperty}")
            });
            
            // only works if you're not sorting by something else that would be displayed instead
            // TODO: does this need to change for full list?
            if (IncludePronouns && (SortProperty == SortProperty.Name || SortProperty == SortProperty.DisplayName || SortProperty == SortProperty.Random))
                str.Append(", including pronouns");

            if (Search != null)
            {
                str.Append($", searching for \"{Search}\"");
                if (SearchDescription) str.Append(" (including description)");
            }

            str.Append(PrivacyFilter switch
            {
                null => ", showing all members",
                PrivacyLevel.Private => ", showing only private members",
                PrivacyLevel.Public => "", // (default, no extra line needed)
                _ => new ArgumentOutOfRangeException($"Couldn't find readable string for privacy filter {PrivacyFilter}")
            });

            return str.ToString();
        }

        public DatabaseViewsExt.MemberListQueryOptions ToQueryOptions() =>
            new DatabaseViewsExt.MemberListQueryOptions
            {
                PrivacyFilter = PrivacyFilter, 
                GroupFilter = GroupFilter,
                Search = Search,
                SearchDescription = SearchDescription
            };
    }

    public static class MemberListOptionsExt
    {
        public static IEnumerable<ListedMember> SortByMemberListOptions(this IEnumerable<ListedMember> input, MemberListOptions opts, LookupContext ctx)
        {
            IComparer<T> ReverseMaybe<T>(IComparer<T> c) =>
                opts.Reverse ? Comparer<T>.Create((a, b) => c.Compare(b, a)) : c;
            
            var randGen = new global::System.Random();

            var culture = StringComparer.InvariantCultureIgnoreCase;
            return (opts.SortProperty switch
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
                SortProperty.Random => input
                    .OrderBy(m => randGen.Next()),
                _ => throw new ArgumentOutOfRangeException($"Unknown sort property {opts.SortProperty}")
            })
                // Lastly, add a by-name fallback order for collisions (generally hits w/ lots of null values)
                .ThenBy(m => m.NameFor(ctx), culture);
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
        Birthdate,
        Random
    }

    public enum ListType
    {
        Short,
        Long
    }
}