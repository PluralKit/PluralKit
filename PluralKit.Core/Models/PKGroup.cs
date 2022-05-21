using Newtonsoft.Json.Linq;

using NodaTime;

namespace PluralKit.Core;

public readonly struct GroupId: INumericId<GroupId, int>
{
    public int Value { get; }

    public GroupId(int value)
    {
        Value = value;
    }

    public bool Equals(GroupId other) => Value == other.Value;

    public override bool Equals(object obj) => obj is GroupId other && Equals(other);

    public override int GetHashCode() => Value;

    public static bool operator ==(GroupId left, GroupId right) => left.Equals(right);

    public static bool operator !=(GroupId left, GroupId right) => !left.Equals(right);

    public int CompareTo(GroupId other) => Value.CompareTo(other.Value);

    public override string ToString() => $"Group #{Value}";
}

#nullable enable
public class PKGroup
{
    public GroupId Id { get; private set; }
    public string Hid { get; private set; } = null!;
    public Guid Uuid { get; private set; }
    public SystemId System { get; private set; }

    public string Name { get; private set; } = null!;
    public string? DisplayName { get; private set; }
    public string? Description { get; private set; }
    public string? Icon { get; private set; }
    public string? BannerImage { get; private set; }
    public string? Color { get; private set; }

    public PrivacyLevel NamePrivacy { get; private set; }
    public PrivacyLevel DescriptionPrivacy { get; private set; }
    public PrivacyLevel IconPrivacy { get; private set; }
    public PrivacyLevel ListPrivacy { get; private set; }
    public PrivacyLevel MetadataPrivacy { get; private set; }
    public PrivacyLevel Visibility { get; private set; }

    public Instant Created { get; private set; }
}

public static class PKGroupExt
{
    public static string? NameFor(this PKGroup group, LookupContext ctx) =>
        group.NamePrivacy.Get(ctx, group.Name, group.DisplayName ?? group.Name);

    public static string? DescriptionFor(this PKGroup group, LookupContext ctx) =>
        group.DescriptionPrivacy.Get(ctx, group.Description);

    public static string? IconFor(this PKGroup group, LookupContext ctx) =>
        group.IconPrivacy.Get(ctx, group.Icon?.TryGetCleanCdnUrl());

    public static Instant? CreatedFor(this PKGroup group, LookupContext ctx) =>
        group.MetadataPrivacy.Get(ctx, (Instant?)group.Created);

    public static JObject ToJson(this PKGroup group, LookupContext ctx, string? systemStr = null,
                                 bool needsMembersArray = false)
    {
        var o = new JObject();

        o.Add("id", group.Hid);
        o.Add("uuid", group.Uuid.ToString());
        o.Add("name", group.NameFor(ctx));

        if (systemStr != null)
            o.Add("system", systemStr);

        o.Add("display_name", group.NamePrivacy.CanAccess(ctx) ? group.DisplayName : null);
        o.Add("description", group.DescriptionPrivacy.Get(ctx, group.Description));
        o.Add("icon", group.IconFor(ctx));
        o.Add("banner", group.DescriptionPrivacy.Get(ctx, group.BannerImage));
        o.Add("color", group.Color);

        o.Add("created", group.CreatedFor(ctx)?.FormatExport());

        if (needsMembersArray)
            o.Add("members", new JArray());

        if (ctx == LookupContext.ByOwner)
        {
            var p = new JObject();

            p.Add("name_privacy", group.NamePrivacy.ToJsonString());
            p.Add("description_privacy", group.DescriptionPrivacy.ToJsonString());
            p.Add("icon_privacy", group.IconPrivacy.ToJsonString());
            p.Add("list_privacy", group.ListPrivacy.ToJsonString());
            p.Add("metadata_privacy", group.MetadataPrivacy.ToJsonString());
            p.Add("visibility", group.Visibility.ToJsonString());

            o.Add("privacy", p);
        }
        else
        {
            o.Add("privacy", null);
        }

        return o;
    }
}