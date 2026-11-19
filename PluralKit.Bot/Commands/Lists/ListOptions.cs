using System.Text;

using Humanizer;

using NodaTime;

using PluralKit.Core;

#nullable enable
namespace PluralKit.Bot;

public class ListOptions(ListEntity entityType, bool isOwner)
{
    private SortOption? _sortProperty { get; set; }
    public SortOption SortProperty
    {
        get => _sortProperty ?? ListEntityField.Name;
        set
        {
            if (_sortProperty != null)
                throw new PKError("Cannot sort in multiple ways at the same time. Please choose only one sorting method.");

            if (!SortOptions.Any(x => x == value))
                throw new PKError($"Sort option \"{value.ToReadableString()}\" is not valid for {entityType.ToString().ToLowerInvariant()} lists.");

            _sortProperty = value;
        }
    }

    public bool Reverse { get; set; }

    public PrivacyLevel? PrivacyFilter { get; set; } = PrivacyLevel.Public;
    public LookupContext Context { get; set; } = LookupContext.ByNonOwner;
    public bool IsOwner { get; } = isOwner;
    public GroupId? GroupFilter { get; set; }
    public MemberId? MemberFilter { get; set; }
    public string? Search { get; set; }
    public bool SearchDescription { get; set; }

    public ListType Type { get; set; }

    public ImmutableDict<ListEntityField, bool> IncludedFields { get; } = entityType switch
    {
        ListEntity.Member => new()
        {
            {ListEntityField.DisplayName, default},
            {ListEntityField.Pronouns, default},
            {ListEntityField.Birthdate, default},
            {ListEntityField.ProxyTags, default},
            {ListEntityField.MessageCount, default},
            {ListEntityField.LastMessage, default},
            {ListEntityField.LastSwitch, default},
            {ListEntityField.CreationDate, default},
            {ListEntityField.Avatar, default},
            {ListEntityField.Description, default},
            {ListEntityField.Groups, default},
            {ListEntityField.Aliases, default}
        },
        ListEntity.Group => new()
        {
            {ListEntityField.DisplayName, default},
            {ListEntityField.CreationDate, default},
            {ListEntityField.Avatar, default},
            {ListEntityField.Description, default},
            {ListEntityField.MemberCount, default},
            {ListEntityField.MessageCount, default}
        },
        _ => []
    };

    public ImmutableDict<ListEntityField, bool> WithFilter { get; } = entityType switch
    {
        ListEntity.Member => new()
        {
            {ListEntityField.Avatar, default},
            {ListEntityField.Pronouns, default},
            {ListEntityField.DisplayName, default},
            {ListEntityField.Birthdate, default},
            {ListEntityField.ProxyTags, default},
            {ListEntityField.Groups, default},
            {ListEntityField.Aliases, default},
            {ListEntityField.Description, default},
            {ListEntityField.Color, default}
        },
        ListEntity.Group => new()
        {
            {ListEntityField.Avatar, default},
            {ListEntityField.DisplayName, default},
            {ListEntityField.Members, default},
            {ListEntityField.Description, default},
            {ListEntityField.Color, default},
        },
        _ => []
    };
    public ImmutableDict<ListEntityField, bool> WithOutFilter { get; } = entityType switch
    {
        ListEntity.Member => new()
        {
            {ListEntityField.Avatar, default},
            {ListEntityField.Pronouns, default},
            {ListEntityField.DisplayName, default},
            {ListEntityField.Birthdate, default},
            {ListEntityField.ProxyTags, default},
            {ListEntityField.Groups, default},
            {ListEntityField.Aliases, default},
            {ListEntityField.Description, default},
            {ListEntityField.Color, default}
        },
        ListEntity.Group => new()
        {
            {ListEntityField.Avatar, default},
            {ListEntityField.DisplayName, default},
            {ListEntityField.Members, default},
            {ListEntityField.Description, default},
            {ListEntityField.Color, default},
        },
        _ => []
    };

    public IEnumerable<SortOption> SortOptions { get; } = entityType switch
    {
        ListEntity.Member => [
            ListEntityField.Name,
            ListEntityField.DisplayName,
            ListEntityField.Hid,
            ListEntityField.MessageCount,
            ListEntityField.CreationDate,
            ListEntityField.LastSwitch,
            ListEntityField.LastMessage,
            ListEntityField.Birthdate,
            ExtraSortOption.FullBirthdate,
            ExtraSortOption.Random,
        ],
        ListEntity.Group => [
            ListEntityField.Name,
            ListEntityField.DisplayName,
            ListEntityField.Hid,
            ListEntityField.CreationDate,
            ListEntityField.MemberCount,
            ListEntityField.MessageCount,
            ExtraSortOption.Random,
        ],
        _ => []
    };

    public bool ShowIds { get; set; } = true;
    public bool NumberItems { get; set; } = false;

    public bool SortIncludeMatch { get; set; } = false;

    public int IncludedCount => IncludedFields.Sum(x => Convert.ToInt32(x.Value));

    // Reset the options that can change while the list is pulled up
    public void ResetSort()
    {
        _sortProperty = null;
    }
    public void ResetIncludes()
    {
        foreach (var key in IncludedFields.Keys)
            IncludedFields[key] = false;
        SortIncludeMatch = false;
    }
    public void ResetWithFilter()
    {
        foreach (var key in WithFilter.Keys)
            WithFilter[key] = false;
    }
    public void ResetWithOutFilter()
    {
        foreach (var key in WithOutFilter.Keys)
            WithOutFilter[key] = false;
    }

    public string CreateFilterString()
    {
        var str = new StringBuilder();
        str.Append("Sorting ");
        if (SortProperty == ExtraSortOption.Random) str.Append("randomly");
        else
        {
            str.Append($"by {SortProperty.ToReadableString()}");
        }

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
            str.Append($" Excluding members with {string.Join(", ", WithOutFilter.Where(x => x.Value).SkipLast(1).Select(x => x.Key.ToReadableString(true)))}");
            str.Append($"{(WithOutFilter.Count(x => x.Value) == 2 ? "" : ",")} or {WithOutFilter.Last(x => x.Value).Key.ToReadableString(true)}.");
        }
        else if (WithOutFilter.Any(x => x.Value))
            str.Append($" Excluding members with {WithOutFilter.First(x => x.Value).Key.ToReadableString(true)}.");

        if (WithFilter.Count(x => x.Value) > 1)
        {
            str.Append($" Excluding members without {string.Join(", ", WithFilter.Where(x => x.Value).SkipLast(1).Select(x => x.Key.ToReadableString(true)))}");
            str.Append($"{(WithFilter.Count(x => x.Value) == 2 ? "" : ",")} or {WithFilter.Last(x => x.Value).Key.ToReadableString(true)}.");
        }
        else if (WithFilter.Any(x => x.Value))
            str.Append($" Excluding members without {WithFilter.First(x => x.Value).Key.ToReadableString(true)}.");

        return str.ToString();
    }
}

public static class ListOptionsExt
{
    public static IEnumerable<ListedMember> SortAndFilterByListOptions(this IEnumerable<ListedMember> input,
                                                                    ListOptions opts)
    {
        input = input.Where(m =>
        {
            foreach (var with in opts.WithFilter.Where(x => x.Value))
            {
                switch (with.Key)
                {
                    case ListEntityField.Avatar:
                        if (!(m.AvatarFor(opts.Context) is { }))
                            return false;
                        break;
                    case ListEntityField.Pronouns:
                        if (!(m.PronounsFor(opts.Context) is { }))
                            return false;
                        break;
                    case ListEntityField.DisplayName:
                        if (!m.NamePrivacy.CanAccess(opts.Context) || m.DisplayName == null)
                            return false;
                        break;
                    case ListEntityField.Birthdate:
                        if (!(m.BirthdayFor(opts.Context) is { }))
                            return false;
                        break;
                    case ListEntityField.ProxyTags:
                        if (!(m.ProxyTagsFor(opts.Context) is { } tags) || tags.Count == 0)
                            return false;
                        break;
                    case ListEntityField.Aliases:
                        if (!(m.AliasesFor(opts.Context) is { } aliases) || aliases.Count == 0)
                            return false;
                        break;
                    case ListEntityField.Description:
                        if (!(m.DescriptionFor(opts.Context) is { }))
                            return false;
                        break;
                    case ListEntityField.Color:
                        if (m.Color == null)
                            return false;
                        break;
                    case ListEntityField.Groups:
                        if (!(m.GroupIds is { } groups) || groups.Count == 0)
                            return false;
                        break;
                }
            }
            foreach (var without in opts.WithOutFilter.Where(x => x.Value))
            {
                switch (without.Key)
                {
                    case ListEntityField.Avatar:
                        if (m.AvatarFor(opts.Context) is { })
                            return false;
                        break;
                    case ListEntityField.Pronouns:
                        if (m.PronounsFor(opts.Context) is { })
                            return false;
                        break;
                    case ListEntityField.DisplayName:
                        if (m.NamePrivacy.CanAccess(opts.Context) && m.DisplayName != null)
                            return false;
                        break;
                    case ListEntityField.Birthdate:
                        if (m.BirthdayFor(opts.Context) is { })
                            return false;
                        break;
                    case ListEntityField.ProxyTags:
                        if (m.ProxyTagsFor(opts.Context) is { } tags && tags.Count > 0)
                            return false;
                        break;
                    case ListEntityField.Aliases:
                        if (m.AliasesFor(opts.Context) is { } aliases && aliases.Count > 0)
                            return false;
                        break;
                    case ListEntityField.Description:
                        if (m.DescriptionFor(opts.Context) is { })
                            return false;
                        break;
                    case ListEntityField.Color:
                        if (m.Color != null)
                            return false;
                        break;
                    case ListEntityField.Groups:
                        if (m.GroupIds is { } groups && groups.Count > 0)
                            return false;
                        break;
                }
            }
            if (opts.PrivacyFilter != null && m.MemberVisibility != opts.PrivacyFilter)
                return false;


            if (opts.Search == null)
                return true;

            var comparer = StringComparison.InvariantCultureIgnoreCase;
            if (m.NameFor(opts.Context).Contains(opts.Search, comparer))
                return true;
            if (m.DisplayName != null && m.DisplayName.Contains(opts.Search, comparer))
                return true;
            if (opts.SearchDescription && m.DescriptionFor(opts.Context) is { } desc && desc.Contains(opts.Search, comparer))
                return true;
            foreach (var alias in m.AliasesFor(opts.Context) ?? [])
                if (alias.Contains(opts.Search, comparer))
                    return true;
            return false;
        });

        IComparer<T> ReverseMaybe<T>(IComparer<T> c) =>
            opts.Reverse ? Comparer<T>.Create((a, b) => c.Compare(b, a)) : c;

        var randGen = new global::System.Random();
        var culture = StringComparer.InvariantCultureIgnoreCase;
        return (opts.SortProperty.value switch
        {
            // As for the OrderByDescending HasValue calls: https://www.jerriepelser.com/blog/orderby-with-null-values/
            // We want nulls last no matter what, even if orders are reversed
            (ListEntityField)ExtraSortOption.Random => input.OrderBy(m => randGen.Next()),
            (ListEntityField)ExtraSortOption.FullBirthdate => input
                // Sort: Birthdays with year -> Birthdays without year -> No Birthdays
                .OrderBy(m => m.BirthdayFor(opts.Context) is { } bday ? (bday.Year == 0 || bday.Year == 4 ? 1 : 0) : 2)
                .ThenBy(m => m.BirthdayFor(opts.Context), ReverseMaybe(Comparer<LocalDate?>.Default)),
            ListEntityField.Hid => input.OrderBy(m => m.Hid, ReverseMaybe(culture)),
            ListEntityField.Name => input.OrderBy(m => m.NameFor(opts.Context), ReverseMaybe(culture)),
            ListEntityField.CreationDate => input
                .OrderByDescending(m => m.MetadataPrivacy.CanAccess(opts.Context))
                .ThenBy(m => m.MetadataPrivacy.Get(opts.Context, m.Created, default), ReverseMaybe(Comparer<Instant>.Default)),
            ListEntityField.MessageCount => input
                .OrderByDescending(m => m.MetadataPrivacy.CanAccess(opts.Context))
                .ThenByDescending(m => m.MetadataPrivacy.Get(opts.Context, m.MessageCount, 0), ReverseMaybe(Comparer<int>.Default)),
            ListEntityField.DisplayName => input
                .OrderByDescending(m => m.DisplayName != null && m.NamePrivacy.CanAccess(opts.Context))
                .ThenBy(m => m.NamePrivacy.Get(opts.Context, m.DisplayName), ReverseMaybe(culture)),
            ListEntityField.Birthdate => input
                .OrderByDescending(m => m.AnnualBirthday.HasValue && m.BirthdayPrivacy.CanAccess(opts.Context))
                .ThenBy(m => m.BirthdayPrivacy.Get(opts.Context, m.AnnualBirthday), ReverseMaybe(Comparer<AnnualDate?>.Default)),
            ListEntityField.LastMessage => input
                .OrderByDescending(m => m.LastMessageTimestamp.HasValue)
                .ThenByDescending(m => m.LastMessageTimestamp, ReverseMaybe(Comparer<Instant?>.Default)),
            ListEntityField.LastSwitch => input
                .OrderByDescending(m => m.LastSwitchTime.HasValue && m.MetadataPrivacy.CanAccess(opts.Context))
                .ThenByDescending(m => m.MetadataPrivacy.Get(opts.Context, m.LastSwitchTime), ReverseMaybe(Comparer<Instant?>.Default)),
            _ => throw new PKError($"Unknown or invalid sort property {opts.SortProperty}")
        })
            // Lastly, add a by-name fallback order for collisions (generally hits w/ lots of null values)
            .ThenBy(m => m.NameFor(opts.Context), culture);
    }

    public static IEnumerable<ListedGroup> SortAndFilterByListOptions(this IEnumerable<ListedGroup> input,
                                                                ListOptions opts)
    {
        input = input.Where(g =>
        {
            foreach (var with in opts.WithFilter.Where(x => x.Value))
            {
                switch (with.Key)
                {
                    case ListEntityField.Avatar:
                        if (!(g.IconFor(opts.Context) is { }))
                            return false;
                        break;
                    case ListEntityField.DisplayName:
                        if (!g.NamePrivacy.CanAccess(opts.Context) || g.DisplayName == null)
                            return false;
                        break;
                    case ListEntityField.Description:
                        if (!(g.DescriptionFor(opts.Context) is { }))
                            return false;
                        break;
                    case ListEntityField.Color:
                        if (g.Color == null)
                            return false;
                        break;
                    case ListEntityField.Members:
                        if (!g.ListPrivacy.CanAccess(opts.Context) || g.MemberCount(opts.PrivacyFilter) == 0)
                            return false;
                        break;
                }
            }
            foreach (var without in opts.WithOutFilter.Where(x => x.Value))
            {
                switch (without.Key)
                {
                    case ListEntityField.Avatar:
                        if (g.IconFor(opts.Context) is { })
                            return false;
                        break;
                    case ListEntityField.DisplayName:
                        if (g.NamePrivacy.CanAccess(opts.Context) && g.DisplayName != null)
                            return false;
                        break;
                    case ListEntityField.Description:
                        if (g.DescriptionFor(opts.Context) is { })
                            return false;
                        break;
                    case ListEntityField.Color:
                        if (g.Color != null)
                            return false;
                        break;
                    case ListEntityField.Members:
                        if (g.ListPrivacy.CanAccess(opts.Context) && g.MemberCount(opts.PrivacyFilter) != 0)
                            return false;
                        break;
                }
            }
            if (opts.PrivacyFilter != null && g.Visibility != opts.PrivacyFilter)
                return false;


            if (opts.Search == null)
                return true;

            var comparer = StringComparison.InvariantCultureIgnoreCase;
            if (g.NameFor(opts.Context).Contains(opts.Search, comparer))
                return true;
            if (g.DisplayName != null && g.DisplayName.Contains(opts.Search, comparer))
                return true;
            if (opts.SearchDescription && g.DescriptionFor(opts.Context) is { } desc && desc.Contains(opts.Search, comparer))
                return true;
            return false;
        });

        IComparer<T> ReverseMaybe<T>(IComparer<T> c) =>
            opts.Reverse ? Comparer<T>.Create((a, b) => c.Compare(b, a)) : c;

        var culture = StringComparer.InvariantCultureIgnoreCase;
        var randGen = new global::System.Random();

        return (opts.SortProperty.value switch
        {
            // As for the OrderByDescending HasValue calls: https://www.jerriepelser.com/blog/orderby-with-null-values/
            // We want nulls last no matter what, even if orders are reversed
            (ListEntityField)ExtraSortOption.Random => input
                .OrderBy(g => randGen.Next()),
            ListEntityField.Hid => input.OrderBy(g => g.Hid, ReverseMaybe(culture)),
            ListEntityField.Name => input.OrderBy(g => g.NameFor(opts.Context), ReverseMaybe(culture)),
            ListEntityField.CreationDate => input
                .OrderByDescending(g => g.MetadataPrivacy.CanAccess(opts.Context))
                .ThenBy(g => g.MetadataPrivacy.Get(opts.Context, g.Created, default), ReverseMaybe(Comparer<Instant>.Default)),
            ListEntityField.DisplayName => input
                .OrderByDescending(g => g.DisplayName != null && g.NamePrivacy.CanAccess(opts.Context))
                .ThenBy(g => g.NamePrivacy.Get(opts.Context, g.DisplayName), ReverseMaybe(culture)),
            ListEntityField.MemberCount => input.OrderByDescending(g => g.MemberCount(opts.PrivacyFilter), ReverseMaybe(Comparer<int>.Default)),
            ListEntityField.MessageCount => input.OrderByDescending(g => g.MessageCount(opts.Context, opts.PrivacyFilter), ReverseMaybe(Comparer<int>.Default)),
            _ => throw new PKError($"Unknown or invalid sort property {opts.SortProperty}")
        })
                // Lastly, add a by-name fallback order for collisions (generally hits w/ lots of null values)
                .ThenBy(g => g.NameFor(opts.Context), culture);
    }

    public static void AssertIsValid(this ListOptions opts)
    {
        if (opts.Type == ListType.Short && opts.IncludedCount > 1)
            throw new PKError("The short list does not support showing information from multiple flags. Try using the full list instead.");

        foreach (var with in opts.WithFilter)
            if (with.Value && opts.WithOutFilter[with.Key])
                throw new PKError($"Cannot include both \"only members with {with.Key.ToReadableString(true)}\" and \"only members without {with.Key.ToReadableString(true)}\". " +
                $"To show members both with and without {with.Key.ToReadableString(true)}, select neither option.");

        if (!opts.IsOwner && (opts.Context == LookupContext.ByOwner || opts.PrivacyFilter != PrivacyLevel.Public))
            throw Errors.NotOwnInfo;

        // the check for multiple *sorting* property flags is done in SortProperty setter
    }
}

public enum ListType { Short, Long }
public enum ListEntity { Member, Group }

public class ImmutableDict<TKey, TValue>: Dictionary<TKey, TValue> where TKey : notnull
{
    public new TValue this[TKey key]
    {
        get => (this as Dictionary<TKey, TValue>)[key];
        set
        {
            if (ContainsKey(key))
                (this as Dictionary<TKey, TValue>)[key] = value;
        }
    }
}