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

public static class ListEntityFieldExtensions
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
        _ => throw new ArgumentOutOfRangeException(nameof(field))
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
        _ => throw new ArgumentOutOfRangeException($"Couldn't find readable string for sort property {field}")
    };
}

// Enums can't have inheritence so this is the alternative
public readonly struct SortOption(ListEntityField entityField): IEquatable<SortOption>
{
    // Add SortOptions that are not also EntityFields here
    public static readonly SortOption Random = new((ListEntityField)(-1));

    private readonly ListEntityField entityField = entityField;
    public ListEntityField? Field => Enum.IsDefined(entityField) ? entityField : null;

    public bool Equals(SortOption other) => entityField == other.entityField;
    public override bool Equals(object? obj) => obj is SortOption other && Equals(other);
    public static bool operator ==(SortOption left, SortOption right) => left.Equals(right);
    public static bool operator !=(SortOption left, SortOption right) => !left.Equals(right);
    public static implicit operator SortOption(ListEntityField entityField) => new(entityField);

    public override int GetHashCode() => entityField.GetHashCode();

    // If more SortOptions that are not EntityFields get added also change these
    public string ToLabel() => Field == null ? "Random" : Field?.ToLabel();
    public override string ToString() => Field == null ? "Random" : Field.ToString();
    public string ToReadableString() => Field == null ? "random" : Field?.ToReadableString();
}