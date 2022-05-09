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
    public Partial<string[]> DescriptionTemplates { get; set; }


    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("ui_tz", UiTz)
        .With("pings_enabled", PingsEnabled)
        .With("latch_timeout", LatchTimeout)
        .With("member_default_private", MemberDefaultPrivate)
        .With("group_default_private", GroupDefaultPrivate)
        .With("show_private_info", ShowPrivateInfo)
        .With("member_limit_override", MemberLimitOverride)
        .With("group_limit_override", GroupLimitOverride)
        .With("description_templates", DescriptionTemplates)
    );

    public new void AssertIsValid()
    {
        if (UiTz.IsPresent && DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz.Value) == null)
            Errors.Add(new ValidationError("timezone"));

        if (DescriptionTemplates.IsPresent)
        {
            if (DescriptionTemplates.Value.Length > 3)
                Errors.Add(new FieldTooLongError("description_templates", 3, DescriptionTemplates.Value.Length));

            foreach (var template in DescriptionTemplates.Value)
                if (template.Length > Limits.MaxDescriptionLength)
                    Errors.Add(new FieldTooLongError($"description_templates[{Array.IndexOf(DescriptionTemplates.Value, template)}]", template.Length, Limits.MaxDescriptionLength));
        }
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

        if (DescriptionTemplates.IsPresent)
            o.Add("description_templates", JArray.FromObject(DescriptionTemplates.Value));

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

        if (o.ContainsKey("description_templates"))
            patch.DescriptionTemplates = o.Value<JArray>("description_templates").Select(x => x.Value<string>()).ToArray();

        return patch;
    }
}