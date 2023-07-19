using Dapper.Contrib.Extensions;

using Newtonsoft.Json.Linq;

using NodaTime;

namespace PluralKit.Core;

public readonly struct SystemId: INumericId<SystemId, int>
{
    public int Value { get; }

    public SystemId(int value)
    {
        Value = value;
    }

    public bool Equals(SystemId other) => Value == other.Value;

    public override bool Equals(object obj) => obj is SystemId other && Equals(other);

    public override int GetHashCode() => Value;

    public static bool operator ==(SystemId left, SystemId right) => left.Equals(right);

    public static bool operator !=(SystemId left, SystemId right) => !left.Equals(right);

    public int CompareTo(SystemId other) => Value.CompareTo(other.Value);

    public override string ToString() => $"System #{Value}";
}

public class PKSystem
{
    [Key] public SystemId Id { get; }
    public string Hid { get; }
    public Guid Uuid { get; private set; }
    public string Name { get; }
    public string Description { get; }
    public string Tag { get; }
    public string Pronouns { get; }
    public string AvatarUrl { get; }
    public string BannerImage { get; }
    public string Color { get; }
    public string Token { get; }
    public string WebhookUrl { get; }
    public string WebhookToken { get; }
    public Instant Created { get; }
    public PrivacyLevel NamePrivacy { get; }
    public PrivacyLevel AvatarPrivacy { get; }
    public PrivacyLevel DescriptionPrivacy { get; }
    public PrivacyLevel MemberListPrivacy { get; }
    public PrivacyLevel FrontPrivacy { get; }
    public PrivacyLevel FrontHistoryPrivacy { get; }
    public PrivacyLevel GroupListPrivacy { get; }
    public PrivacyLevel PronounPrivacy { get; }
}

public static class PKSystemExt
{
    public static string DescriptionFor(this PKSystem system, LookupContext ctx) =>
        system.DescriptionPrivacy.Get(ctx, system.Description);

    public static string NameFor(this PKSystem system, LookupContext ctx) =>
        system.NamePrivacy.Get(ctx, system.Name);

    public static string AvatarFor(this PKSystem system, LookupContext ctx) =>
        system.AvatarPrivacy.Get(ctx, system.AvatarUrl.TryGetCleanCdnUrl());

    public static JObject ToJson(this PKSystem system, LookupContext ctx)
    {
        var o = new JObject();
        o.Add("id", system.Hid);
        o.Add("uuid", system.Uuid.ToString());

        o.Add("name", system.NameFor(ctx));
        o.Add("description", system.DescriptionFor(ctx));
        o.Add("tag", system.Tag);
        o.Add("pronouns", system.PronounPrivacy.Get(ctx, system.Pronouns));

        o.Add("avatar_url", system.AvatarFor(ctx));
        o.Add("banner", system.DescriptionPrivacy.Get(ctx, system.BannerImage).TryGetCleanCdnUrl());
        o.Add("color", system.Color);
        o.Add("created", system.Created.FormatExport());

        if (ctx == LookupContext.ByOwner)
        {
            // todo: should this be moved to a different JSON model?
            o.Add("webhook_url", system.WebhookUrl);
            // o.Add("webhook_token", system.WebhookToken);

            var p = new JObject();

            p.Add("name_privacy", system.NamePrivacy.ToJsonString());
            p.Add("avatar_privacy", system.AvatarPrivacy.ToJsonString());
            p.Add("description_privacy", system.DescriptionPrivacy.ToJsonString());
            p.Add("pronoun_privacy", system.PronounPrivacy.ToJsonString());
            p.Add("member_list_privacy", system.MemberListPrivacy.ToJsonString());
            p.Add("group_list_privacy", system.GroupListPrivacy.ToJsonString());
            p.Add("front_privacy", system.FrontPrivacy.ToJsonString());
            p.Add("front_history_privacy", system.FrontHistoryPrivacy.ToJsonString());

            o.Add("privacy", p);
        }
        else
        {
            o.Add("privacy", null);
        }

        return o;
    }
}