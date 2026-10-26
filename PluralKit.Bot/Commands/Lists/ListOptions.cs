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
    public LookupContext Context { get; set; } = LookupContext.ByNonOwner;
    public GroupId? GroupFilter { get; set; }
    public MemberId? MemberFilter { get; set; }
    public string? Search { get; set; }
    public bool SearchDescription { get; set; }

    public ListType Type { get; set; }

    public Dictionary<EntityField, bool> IncludeValues { get; } = new()
    {
        {EntityField.DisplayName, default},
        {EntityField.Pronouns, default},
        {EntityField.Birthdate, default},
        {EntityField.ProxyTags, default},
        {EntityField.MessageCount, default},
        {EntityField.LastMessage, default},
        {EntityField.LastSwitch, default},
        {EntityField.CreationDate, default},
        {EntityField.Avatar, default},
        {EntityField.Description, default},
        {EntityField.Groups, default},
        {EntityField.Aliases, default},
    };

    public Dictionary<EntityField, bool> WithFilter { get; } = new()
    {
        {EntityField.Avatar, default},
        {EntityField.Pronouns, default},
        {EntityField.DisplayName, default},
        {EntityField.Birthdate, default},
        {EntityField.ProxyTags, default},
        // {EntityField.Groups, default},
        {EntityField.Aliases, default},
        {EntityField.Description, default},
        {EntityField.Color, default},
    };
    public Dictionary<EntityField, bool> WithOutFilter { get; } = new()
    {
        {EntityField.Avatar, default},
        {EntityField.Pronouns, default},
        {EntityField.DisplayName, default},
        {EntityField.Birthdate, default},
        {EntityField.ProxyTags, default},
        // {EntityField.Groups, default},
        {EntityField.Aliases, default},
        {EntityField.Description, default},
        {EntityField.Color, default},
    };


    public bool ShowIds { get; set; } = true;
    public bool NumberItems { get; set; } = false;

    public bool SortIncludeMatch { get; set; } = false;

    public int IncludedCount => IncludeValues.Sum(x => Convert.ToInt32(x.Value));

    // Reset the options that can change while the list is pulled up without resetting the ones that don't
    public void ResetDynamicOptions()
    {
        foreach (var key in IncludeValues.Keys)
            IncludeValues[key] = false;
        foreach (var key in WithFilter.Keys)
            WithFilter[key] = false;
        foreach (var key in WithOutFilter.Keys)
            WithOutFilter[key] = false;

        _sortProperty = null;
        SortIncludeMatch = false;
    }

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
            null => ", showing public and private items.",
            PrivacyLevel.Private => ", showing only private items.",
            PrivacyLevel.Public => ".", // (default, no extra line needed)
            _ => new ArgumentOutOfRangeException(
                $"Couldn't find readable string for privacy filter {PrivacyFilter}")
        });

        if (WithOutFilter.Count(x => x.Value) > 1)
        {
            str.Append($" Excluding members with {string.Join(", ", WithOutFilter.Where(x => x.Value).SkipLast(1).Select(x => x.Key.ToString()))}");
            str.Append($"{(WithOutFilter.Count(x => x.Value) == 2 ? "" : ",")} or {WithOutFilter.Last(x => x.Value).Key}.");
        }
        else if (WithOutFilter.Any(x => x.Value))
            str.Append($" Excluding members with {WithOutFilter.First(x => x.Value).Key}.");

        if (WithFilter.Count(x => x.Value) > 1)
        {
            str.Append($" Excluding members without {string.Join(", ", WithFilter.Where(x => x.Value).SkipLast(1).Select(x => x.Key.ToString()))}");
            str.Append($"{(WithFilter.Count(x => x.Value) == 2 ? "" : ",")} or {WithFilter.Last(x => x.Value).Key}.");
        }
        else if (WithFilter.Any(x => x.Value))
            str.Append($" Excluding members without {WithFilter.First(x => x.Value).Key}.");

        return str.ToString();
    }

    public DatabaseViewsExt.ListQueryOptions ToQueryOptions() =>
        new()
        {
            PrivacyFilter = PrivacyFilter,
            GroupFilter = GroupFilter,
            MemberFilter = MemberFilter,
            Search = Search,
            SearchDescription = SearchDescription,
            Context = Context
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
            .ThenBy(m => m.NameFor(ctx), culture).Select((m, i) => { m.Order = i + 1; return m; });
    }

    public static IEnumerable<ListedMember> FilterByMemberListOptions(this IEnumerable<ListedMember> input,
                                                                    ListOptions opts, LookupContext ctx) =>
        input.Where(m =>
        {
            foreach (var with in opts.WithFilter.Where(x => x.Value))
            {
                switch (with.Key)
                {
                    case EntityField.Avatar:
                        if (!(m.AvatarFor(ctx) is { }))
                            return false;
                        break;
                    case EntityField.Pronouns:
                        if (!(m.PronounsFor(ctx) is { }))
                            return false;
                        break;
                    case EntityField.DisplayName:
                        if (m.DisplayName == null)
                            return false;
                        break;
                    case EntityField.Birthdate:
                        if (!(m.BirthdayFor(ctx) is { }))
                            return false;
                        break;
                    case EntityField.ProxyTags:
                        if (!(m.ProxyTagsFor(ctx) is { } tags) || tags.Count == 0)
                            return false;
                        break;
                    case EntityField.Aliases:
                        if (!(m.AliasesFor(ctx) is { } aliases) || aliases.Count == 0)
                            return false;
                        break;
                    case EntityField.Description:
                        if (!(m.DescriptionFor(ctx) is { }))
                            return false;
                        break;
                    case EntityField.Color:
                        if (m.Color == null)
                            return false;
                        break;
                }
            }
            foreach (var without in opts.WithOutFilter.Where(x => x.Value))
            {
                switch (without.Key)
                {
                    case EntityField.Avatar:
                        if (m.AvatarFor(ctx) is { })
                            return false;
                        break;
                    case EntityField.Pronouns:
                        if (m.PronounsFor(ctx) is { })
                            return false;
                        break;
                    case EntityField.DisplayName:
                        if (m.DisplayName != null)
                            return false;
                        break;
                    case EntityField.Birthdate:
                        if (m.BirthdayFor(ctx) is { })
                            return false;
                        break;
                    case EntityField.ProxyTags:
                        if (m.ProxyTagsFor(ctx) is { } tags && tags.Count > 0)
                            return false;
                        break;
                    case EntityField.Aliases:
                        if (m.AliasesFor(ctx) is { } aliases && aliases.Count > 0)
                            return false;
                        break;
                    case EntityField.Description:
                        if (m.DescriptionFor(ctx) is { })
                            return false;
                        break;
                    case EntityField.Color:
                        if (m.Color != null)
                            return false;
                        break;
                }
            }
            return true;
        });


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
        if (opts.Type == ListType.Short && opts.IncludedCount > 1)
            throw new PKError("The short list does not support showing information from multiple flags. Try using the full list instead.");

        foreach (var with in opts.WithFilter)
            if (with.Value && opts.WithOutFilter[with.Key])
                throw new PKError($"Cannot include both \"only members with {ArticleMaybe(with.Key)}\" and \"only members without {ArticleMaybe(with.Key)}\". " +
                $"To show members both with and without {ArticleMaybe(with.Key)}, select neither option.");

        // the check for multiple *sorting* property flags is done in SortProperty setter

        static string ArticleMaybe(EntityField e) =>
            (e.ToString().Last() == 's' ? "" : "a ") + e;
    }
}

// Enums cannot have inheritance so this can't be a child of EntityField
// Make sure if changing keys/adding keys the key is named the same in both SortProperty and EntityField
// Or the "Same as Sort" info option in the short lists won't work
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

public enum EntityField
{
    DisplayName,
    MessageCount,
    CreationDate,
    LastSwitch,
    LastMessage,
    Birthdate,
    Avatar,
    Pronouns,
    ProxyTags,
    Groups,
    Aliases,
    Description,
    Color
}

public enum ListType { Short, Long }