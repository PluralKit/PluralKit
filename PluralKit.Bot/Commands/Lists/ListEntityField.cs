namespace PluralKit.Bot;

public enum ListEntityField
{
    Name,
    DisplayName,
    Hid,
    MessageCount,
    CreationDate,
    LastSwitch,
    LastMessage,
    Birthdate,
    Avatar,
    Pronouns,
    ProxyTags,
    Groups,
    Members,
    Aliases,
    Description,
    Color,
    MemberCount,
}

public static class ListEntityFieldExt
{
    public static string ToLabel(this ListEntityField field) => field switch
    {
        ListEntityField.Name => "Name",
        ListEntityField.DisplayName => "Display Name",
        ListEntityField.Hid => "ID",
        ListEntityField.MessageCount => "Message Count",
        ListEntityField.CreationDate => "Creation Date",
        ListEntityField.LastSwitch => "Last Switch",
        ListEntityField.LastMessage => "Last Message",
        ListEntityField.Birthdate => "Birthday",
        ListEntityField.Avatar => "Avatar",
        ListEntityField.Pronouns => "Pronouns",
        ListEntityField.ProxyTags => "Proxy Tags",
        ListEntityField.Groups => "Groups",
        ListEntityField.Members => "Members",
        ListEntityField.Aliases => "Aliases",
        ListEntityField.Description => "Description",
        ListEntityField.Color => "Color",
        ListEntityField.MemberCount => "Member Count",
        _ => throw new ArgumentException($"Couldn't find Label for ExtraSortOption {field}")
    };

    public static string ToReadableString(this ListEntityField field, bool plural = false) => field switch
    {
        ListEntityField.Name => plural ? "names" : "name",
        ListEntityField.DisplayName => plural ? "display names" : "display name",
        ListEntityField.Hid => plural ? "IDs" : "ID",
        ListEntityField.MessageCount => plural ? "message counts" : "message count",
        ListEntityField.CreationDate => plural ? "creation dates" : "creation date",
        ListEntityField.LastSwitch => plural ? "last switches" : "last switch",
        ListEntityField.LastMessage => plural ? "last messages" : "last message",
        ListEntityField.Birthdate => plural ? "birthdays" : "birthday",
        ListEntityField.Avatar => plural ? "avatars" : "avatar",
        ListEntityField.Pronouns => "pronouns",
        ListEntityField.ProxyTags => "proxy tags",
        ListEntityField.Groups => "groups",
        ListEntityField.Members => "members",
        ListEntityField.Aliases => "aliases",
        ListEntityField.Description => plural ? "descriptions" : "description",
        ListEntityField.Color => plural ? "colors" : "color",
        ListEntityField.MemberCount => plural ? "member counts" : "member count",
        _ => throw new ArgumentException($"Couldn't find readable string for sort property {field}")
    };
}

public static class ExtraSortOptionsExt
{
    public static string ToLabel(this ExtraSortOption opt) => opt switch
    {
        ExtraSortOption.Random => "Random",
        ExtraSortOption.FullBirthdate => "Birthdate (including year)",
        _ => throw new ArgumentException($"Couldn't find Label for ExtraSortOption {opt}")
    };

    public static string ToReadableString(this ExtraSortOption opt, bool plural = false) => opt switch
    {
        ExtraSortOption.Random => "random",
        ExtraSortOption.FullBirthdate => plural ? "birthdates (including year)" : "birthdate (including year)",
        _ => throw new ArgumentException($"Couldn't find readable string for sort property {opt}")
    };
}

public enum ExtraSortOption
{
    Random = -1,
    FullBirthdate = -2
}

// Enums can't have inheritence so this is the alternative
public readonly record struct SortOption(ListEntityField entityField): IEquatable<SortOption>
{

    public readonly ListEntityField value = entityField;
    public ListEntityField? AsListEntityField => Enum.IsDefined(entityField) ? entityField : null;
    public ExtraSortOption? AsExtraSortOption => Enum.IsDefined((ExtraSortOption)entityField) ? (ExtraSortOption)entityField : null;

    public static implicit operator SortOption(ListEntityField entityField) => new(entityField);
    public static implicit operator SortOption(ExtraSortOption entityField) => new((ListEntityField)entityField);

    //public override int GetHashCode() => entityField.GetHashCode();

    public static bool TryParse(string value, out SortOption result)
    {
        if (Enum.TryParse<ExtraSortOption>(value, out var extraSortResult))
        {
            result = extraSortResult;
            return true;
        }
        if (Enum.TryParse<ListEntityField>(value, out var fieldSortResult))
        {
            result = fieldSortResult;
            return true;
        }
        result = new();
        return false;
    }

    // If more SortOptions that are not EntityFields get added also change these
    public string ToLabel() => AsListEntityField?.ToLabel() ?? AsExtraSortOption?.ToLabel() ??
        throw new ArgumentException();
    public override string ToString() => AsListEntityField?.ToString() ?? AsExtraSortOption?.ToString() ??
        throw new ArgumentException();
    public string ToReadableString(bool plural = false) =>
        AsListEntityField?.ToReadableString(plural) ?? AsExtraSortOption?.ToReadableString(plural) ??
        throw new ArgumentException();
}