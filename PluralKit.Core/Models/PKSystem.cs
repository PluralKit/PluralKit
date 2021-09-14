using Dapper.Contrib.Extensions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;

namespace PluralKit.Core
{

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
        public string Name { get; }
        public string Description { get; }
        public string Tag { get; }
        public string AvatarUrl { get; }
        public string BannerImage { get; }
        public string Color { get; }
        public string Token { get; }
        public Instant Created { get; }
        public string UiTz { get; set; }
        public bool PingsEnabled { get; }
        public int? LatchTimeout { get; }
        public PrivacyLevel DescriptionPrivacy { get; }
        public PrivacyLevel MemberListPrivacy { get; }
        public PrivacyLevel FrontPrivacy { get; }
        public PrivacyLevel FrontHistoryPrivacy { get; }
        public PrivacyLevel GroupListPrivacy { get; }
        public int? MemberLimitOverride { get; }
        public int? GroupLimitOverride { get; }

        [JsonIgnore] public DateTimeZone Zone => DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz);
    }

    public static class PKSystemExt
    {
        public static string DescriptionFor(this PKSystem system, LookupContext ctx) =>
            system.DescriptionPrivacy.Get(ctx, system.Description);

        public static JObject ToJson(this PKSystem system, LookupContext ctx)
        {
            var o = new JObject();
            o.Add("id", system.Hid);
            o.Add("name", system.Name);
            o.Add("description", system.DescriptionFor(ctx));
            o.Add("tag", system.Tag);
            o.Add("avatar_url", system.AvatarUrl.TryGetCleanCdnUrl());
            o.Add("banner", system.DescriptionPrivacy.Get(ctx, system.BannerImage).TryGetCleanCdnUrl());
            o.Add("color", system.Color);
            o.Add("created", system.Created.FormatExport());
            // todo: change this to "timezone"
            o.Add("tz", system.UiTz);
            // todo: just don't include these if not ByOwner
            o.Add("description_privacy", ctx == LookupContext.ByOwner ? system.DescriptionPrivacy.ToJsonString() : null);
            o.Add("member_list_privacy", ctx == LookupContext.ByOwner ? system.MemberListPrivacy.ToJsonString() : null);
            o.Add("front_privacy", ctx == LookupContext.ByOwner ? system.FrontPrivacy.ToJsonString() : null);
            o.Add("front_history_privacy", ctx == LookupContext.ByOwner ? system.FrontHistoryPrivacy.ToJsonString() : null);
            return o;
        }
    }
}
