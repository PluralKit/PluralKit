using Newtonsoft.Json.Linq;

using NodaTime;

using SqlKata;

namespace PluralKit.Core;

public class SystemConfigPatch: PatchObject
{
    public Partial<string> UiTz { get; set; }
    public Partial<bool> PingsEnabled { get; set; }
    public Partial<int?> LatchTimeout { get; set; }
    public Partial<bool> MemberDefaultPrivate { get; set; }
    public Partial<bool> GroupDefaultPrivate { get; set; }
    public Partial<bool> ShowPrivateInfo { get; set; }
    public Partial<int?> MemberLimitOverride { get; set; }
    public Partial<int?> GroupLimitOverride { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("ui_tz", UiTz)
        .With("pings_enabled", PingsEnabled)
        .With("latch_timeout", LatchTimeout)
        .With("member_default_private", MemberDefaultPrivate)
        .With("group_default_private", GroupDefaultPrivate)
        .With("show_private_info", ShowPrivateInfo)
        .With("member_limit_override", MemberLimitOverride)
        .With("group_limit_override", GroupLimitOverride)
    );

    public new void AssertIsValid()
    {
        if (UiTz.IsPresent && DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz.Value) == null)
            Errors.Add(new ValidationError("timezone"));
    }

    public JObject ToJson()
    {
        var o = new JObject();

        if (UiTz.IsPresent)
            o.Add("timezone", UiTz.Value);

        if (PingsEnabled.IsPresent)
            o.Add("pings_enabled", PingsEnabled.Value);

        if (LatchTimeout.IsPresent)
            o.Add("latch_timeout", LatchTimeout.Value);

        if (MemberDefaultPrivate.IsPresent)
            o.Add("member_default_private", MemberDefaultPrivate.Value);

        if (GroupDefaultPrivate.IsPresent)
            o.Add("group_default_private", GroupDefaultPrivate.Value);

        if (ShowPrivateInfo.IsPresent)
            o.Add("show_private_info", ShowPrivateInfo.Value);

        if (MemberLimitOverride.IsPresent)
            o.Add("member_limit", MemberLimitOverride.Value);

        if (GroupLimitOverride.IsPresent)
            o.Add("group_limit", GroupLimitOverride.Value);

        return o;
    }

    public static SystemConfigPatch FromJson(JObject o)
    {
        var patch = new SystemConfigPatch();

        if (o.ContainsKey("timezone"))
            patch.UiTz = o.Value<string>("timezone");

        if (o.ContainsKey("pings_enabled"))
            patch.PingsEnabled = o.Value<bool>("pings_enabled");

        if (o.ContainsKey("latch_timeout"))
            patch.LatchTimeout = o.Value<int?>("latch_timeout");

        if (o.ContainsKey("member_default_private"))
            patch.MemberDefaultPrivate = o.Value<bool>("member_default_private");

        if (o.ContainsKey("group_default_private"))
            patch.GroupDefaultPrivate = o.Value<bool>("group_default_private");

        return patch;
    }
}