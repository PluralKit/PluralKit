using System.Text;

using Humanizer;

using NodaTime;

using PluralKit.Core;

#nullable enable
namespace PluralKit.Bot;

public class ListOptions
{
    private SortProperty? _sortProperty { get; set; }
    public SortProperty SortProperty
    {
        get => _sortProperty ?? SortProperty.Name;
        set
        {
            if (_sortProperty != null)
                throw new PKError("Cannot sort in multiple ways at the same time. Please choose only one sorting method.");

            _sortProperty = value;
        }
    }


    public bool Reverse { get; set; }

    public PrivacyLevel? PrivacyFilter { get; set; } = PrivacyLevel.Public;
    public GroupId? GroupFilter { get; set; }
    public MemberId? MemberFilter { get; set; }
    public string? Search { get; set; }
    public bool SearchDescription { get; set; }

    public ListType Type { get; set; }
    public bool IncludeMessageCount { get; set; }
    public bool IncludeLastSwitch { get; set; }
    public bool IncludeLastMessage { get; set; }
    public bool IncludeCreated { get; set; }
    public bool IncludeAvatar { get; set; }
    public bool IncludePronouns { get; set; }
    public bool IncludeDisplayName { get; set; }
    public bool IncludeBirthday { get; set; }

    // hacky but works, remember to update this when more include flags are added 
    public int includedCount => new[] {
        IncludeMessageCount,
        IncludeLastSwitch,
        IncludeLastMessage,
        IncludeCreated,
        IncludeAvatar,
        IncludePronouns,
        IncludeDisplayName,
        IncludeBirthday,
    }.Sum(x => Convert.ToInt32(x));

    public string CreateFilterString()
    {
        var str = new StringBuilder();
        str.Append("Sorting ");
        if (SortProperty != SortProperty.Random) str.Append("by ");
        str.Append(SortProperty switch
        {
            SortProperty.Name => "name",
            SortProperty.Hid => "ID",
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
            str.Append($", searching for \"{Search.Truncate(100)}\"");
            if (SearchDescription) str.Append(" (including description)");
        }

        str.Append(PrivacyFilter switch
        {
            null => ", showing all items",
            PrivacyLevel.Private => ", showing only private items",
            PrivacyLevel.Public => "", // (default, no extra line needed)
            _ => new ArgumentOutOfRangeException(
                $"Couldn't find readable string for privacy filter {PrivacyFilter}")
        });

        return str.ToString();
    }

    public DatabaseViewsExt.ListQueryOptions ToQueryOptions() =>
        new()
        {
            PrivacyFilter = PrivacyFilter,
            GroupFilter = GroupFilter,
            MemberFilter = MemberFilter,
            Search = Search,
            SearchDescription = SearchDescription
        };
}

public static class ListOptionsExt
{
    public static IEnumerable<ListedMember> SortByMemberListOptions(this IEnumerable<ListedMember> input,
                                                                    ListOptions opts, LookupContext ctx)
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
                .ThenBy(m => m.MetadataPrivacy.Get(ctx, m.Created, default), ReverseMaybe(Comparer<Instant>.Default)),
            SortProperty.MessageCount => input
                .OrderByDescending(m => m.MetadataPrivacy.CanAccess(ctx))
                .ThenByDescending(m => m.MetadataPrivacy.Get(ctx, m.MessageCount, 0), ReverseMaybe(Comparer<int>.Default)),
            SortProperty.DisplayName => input
                .OrderByDescending(m => m.DisplayName != null && m.NamePrivacy.CanAccess(ctx))
                .ThenBy(m => m.NamePrivacy.Get(ctx, m.DisplayName), ReverseMaybe(culture)),
            SortProperty.Birthdate => input
                .OrderByDescending(m => m.AnnualBirthday.HasValue && m.BirthdayPrivacy.CanAccess(ctx))
                .ThenBy(m => m.BirthdayPrivacy.Get(ctx, m.AnnualBirthday), ReverseMaybe(Comparer<AnnualDate?>.Default)),
            SortProperty.LastMessage => input
                .OrderByDescending(m => m.LastMessageTimestamp.HasValue)
                .ThenByDescending(m => m.LastMessageTimestamp, ReverseMaybe(Comparer<Instant?>.Default)),
            SortProperty.LastSwitch => input
                .OrderByDescending(m => m.LastSwitchTime.HasValue && m.MetadataPrivacy.CanAccess(ctx))
                .ThenByDescending(m => m.MetadataPrivacy.Get(ctx, m.LastSwitchTime), ReverseMaybe(Comparer<Instant?>.Default)),
            SortProperty.Random => input
                .OrderBy(m => randGen.Next()),
            _ => throw new ArgumentOutOfRangeException($"Unknown sort property {opts.SortProperty}")
        })
            // Lastly, add a by-name fallback order for collisions (generally hits w/ lots of null values)
            .ThenBy(m => m.NameFor(ctx), culture);
    }

    public static IEnumerable<ListedGroup> SortByGroupListOptions(this IEnumerable<ListedGroup> input,
                                                                    ListOptions opts, LookupContext ctx)
    {
        IComparer<T> ReverseMaybe<T>(IComparer<T> c) =>
            opts.Reverse ? Comparer<T>.Create((a, b) => c.Compare(b, a)) : c;

        var randGen = new global::System.Random();

        var culture = StringComparer.InvariantCultureIgnoreCase;
        return (opts.SortProperty switch
        {
            // As for the OrderByDescending HasValue calls: https://www.jerriepelser.com/blog/orderby-with-null-values/
            // We want nulls last no matter what, even if orders are reversed
            SortProperty.Hid => input.OrderBy(g => g.Hid, ReverseMaybe(culture)),
            SortProperty.Name => input.OrderBy(g => g.NameFor(ctx), ReverseMaybe(culture)),
            SortProperty.CreationDate => input
                .OrderByDescending(g => g.MetadataPrivacy.CanAccess(ctx))
                .ThenBy(g => g.MetadataPrivacy.Get(ctx, g.Created, default), ReverseMaybe(Comparer<Instant>.Default)),
            SortProperty.DisplayName => input
                .OrderByDescending(g => g.DisplayName != null && g.NamePrivacy.CanAccess(ctx))
                .ThenBy(g => g.NamePrivacy.Get(ctx, g.DisplayName), ReverseMaybe(culture)),
            SortProperty.Random => input
                .OrderBy(g => randGen.Next()),
            _ => throw new ArgumentOutOfRangeException($"Unknown sort property {opts.SortProperty}")
        })
                // Lastly, add a by-name fallback order for collisions (generally hits w/ lots of null values)
                .ThenBy(g => g.NameFor(ctx), culture);
    }

    public static void AssertIsValid(this ListOptions opts)
    {
        if (opts.Type == ListType.Short && opts.includedCount > 1)
            throw new PKError("The short list does not support showing information from multiple flags. Try using the full list instead.");

        // the check for multiple *sorting* property flags is done in SortProperty setter
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