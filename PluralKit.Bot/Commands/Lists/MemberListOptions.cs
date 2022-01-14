using System.Text;

using NodaTime;

using PluralKit.Core;

#nullable enable
namespace PluralKit.Bot;

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
            _ => new ArgumentOutOfRangeException(
                $"Couldn't find readable string for privacy filter {PrivacyFilter}")
        });

        return str.ToString();
    }

    public DatabaseViewsExt.MemberListQueryOptions ToQueryOptions() =>
        new()
        {
            PrivacyFilter = PrivacyFilter,
            GroupFilter = GroupFilter,
            Search = Search,
            SearchDescription = SearchDescription
        };
}

public static class MemberListOptionsExt
{
    public static IEnumerable<ListedMember> SortByMemberListOptions(this IEnumerable<ListedMember> input,
                                                                    MemberListOptions opts, LookupContext ctx)
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
            SortProperty.CreationDate => input
                .OrderByDescending(m => m.MetadataPrivacy.CanAccess(ctx))
                .ThenBy(m => m.MetadataPrivacy.CanAccess(ctx) ? m.Created : (Instant)default, 
                    ReverseMaybe(Comparer<Instant>.Default)),
            SortProperty.MessageCount => input
                .OrderBy(m => m.MessageCount == 0 || !m.MetadataPrivacy.CanAccess(ctx))
                .ThenByDescending(m => m.MetadataPrivacy.CanAccess(ctx) ? m.MessageCount : 0, 
                    ReverseMaybe(Comparer<int>.Default)),
            SortProperty.DisplayName => input
                .OrderByDescending(m => m.DisplayName != null && m.NamePrivacy.CanAccess(ctx))
                .ThenBy(m => m.NamePrivacy.CanAccess(ctx) ? m.DisplayName : null, 
                    ReverseMaybe(culture)),
            SortProperty.Birthdate => input
                .OrderByDescending(m => m.AnnualBirthday.HasValue && m.BirthdayPrivacy.CanAccess(ctx))
                .ThenBy(m => m.BirthdayPrivacy.CanAccess(ctx) ? m.AnnualBirthday : null, 
                    ReverseMaybe(Comparer<AnnualDate?>.Default)),
            SortProperty.LastMessage => throw new PKError(
                "Sorting by last message is temporarily disabled due to database issues, sorry."),
            // SortProperty.LastMessage => input
            //     .OrderByDescending(m => m.LastMessage.HasValue)
            //     .ThenByDescending(m => m.LastMessage, ReverseMaybe(Comparer<ulong?>.Default)),
            SortProperty.LastSwitch => input
                .OrderByDescending(m => m.LastSwitchTime.HasValue && m.MetadataPrivacy.CanAccess(ctx))
                .ThenByDescending(m => m.MetadataPrivacy.CanAccess(ctx) ? m.LastSwitchTime : null, 
                    ReverseMaybe(Comparer<Instant?>.Default)),
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

public enum ListType { Short, Long }