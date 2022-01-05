using System.Text;

using NodaTime;

using PluralKit.Core;

#nullable enable
namespace PluralKit.Bot;

public class GroupListOptions
{
    public SortProperty SortProperty { get; set; } = SortProperty.Name;
    public bool Reverse { get; set; }

    public PrivacyLevel? PrivacyFilter { get; set; } = PrivacyLevel.Public;
    public GroupId? GroupFilter { get; set; }
    public string? Search { get; set; }
    public bool SearchDescription { get; set; }

    public ListType Type { get; set; }
    public bool IncludeMessageCount { get; set; }
    public bool IncludeCreated { get; set; }
    public bool IncludeAvatar { get; set; }

    public string CreateFilterString()
    {
        var str = new StringBuilder();
        str.Append("Sorting ");
        if (SortProperty != SortProperty.Random) str.Append("by ");
        str.Append(SortProperty switch
        {
            SortProperty.Name => "group name",
            SortProperty.Hid => "group ID",
            SortProperty.DisplayName => "display name",
            SortProperty.CreationDate => "creation date",
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
            null => ", showing all groups",
            PrivacyLevel.Private => ", showing only private groups",
            PrivacyLevel.Public => "", // (default, no extra line needed)
            _ => new ArgumentOutOfRangeException(
                $"Couldn't find readable string for privacy filter {PrivacyFilter}")
        });

        return str.ToString();
    }

    public DatabaseViewsExt.GroupListQueryOptions ToQueryOptions() =>
        new()
        {
            PrivacyFilter = PrivacyFilter,
            Search = Search,
            SearchDescription = SearchDescription
        };
}

public static class GroupListOptionsExt
{
    public static IEnumerable<ListedGroup> SortByGroupListOptions(this IEnumerable<ListedGroup> input,
                                                                    GroupListOptions opts, LookupContext ctx)
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
            SortProperty.CreationDate => input.OrderBy(g => g.Created, ReverseMaybe(Comparer<Instant>.Default)),
            SortProperty.DisplayName => input
                .OrderByDescending(g => g.DisplayName != null)
                .ThenBy(g => g.DisplayName, ReverseMaybe(culture)),
            SortProperty.Random => input
                .OrderBy(g => randGen.Next()),
            _ => throw new ArgumentOutOfRangeException($"Unknown sort property {opts.SortProperty}")
        })
            // Lastly, add a by-name fallback order for collisions (generally hits w/ lots of null values)
            .ThenBy(m => m.NameFor(ctx), culture);
    }
}