using System.Diagnostics;
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
    public Partial<bool> CaseSensitiveProxyTags { get; set; }
    public Partial<bool> ProxyErrorMessageEnabled { get; set; }
    public Partial<bool> HidDisplaySplit { get; set; }
    public Partial<bool> HidDisplayCaps { get; set; }
    public Partial<string?> NameFormat { get; set; }
    public Partial<SystemConfig.HidPadFormat> HidListPadding { get; set; }
    public Partial<SystemConfig.ProxySwitchAction> ProxySwitch { get; set; }

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
        .With("case_sensitive_proxy_tags", CaseSensitiveProxyTags)
        .With("proxy_error_message_enabled", ProxyErrorMessageEnabled)
        .With("hid_display_split", HidDisplaySplit)
        .With("hid_display_caps", HidDisplayCaps)
        .With("hid_list_padding", HidListPadding)
        .With("proxy_switch", ProxySwitch)
        .With("name_format", NameFormat)
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

        if (CaseSensitiveProxyTags.IsPresent)
            o.Add("case_sensitive_proxy_tags", CaseSensitiveProxyTags.Value);

        if (ProxyErrorMessageEnabled.IsPresent)
            o.Add("proxy_error_message_enabled", ProxyErrorMessageEnabled.Value);

        if (HidDisplaySplit.IsPresent)
            o.Add("hid_display_split", HidDisplaySplit.Value);

        if (HidDisplayCaps.IsPresent)
            o.Add("hid_display_caps", HidDisplayCaps.Value);

        if (HidListPadding.IsPresent)
            o.Add("hid_list_padding", HidListPadding.Value.ToUserString());

        if (ProxySwitch.IsPresent)
            o.Add("proxy_switch", ProxySwitch.Value.ToUserString());

        if (NameFormat.IsPresent)
            o.Add("name_format", NameFormat.Value);

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

        if (o.ContainsKey("case_sensitive_proxy_tags"))
            patch.CaseSensitiveProxyTags = o.Value<bool>("case_sensitive_proxy_tags");

        if (o.ContainsKey("proxy_error_message_enabled"))
            patch.ProxyErrorMessageEnabled = o.Value<bool>("proxy_error_message_enabled");

        if (o.ContainsKey("hid_display_split"))
            patch.HidDisplaySplit = o.Value<bool>("hid_display_split");

        if (o.ContainsKey("hid_display_caps"))
            patch.HidDisplayCaps = o.Value<bool>("hid_display_caps");

        if (o.ContainsKey("proxy_switch"))
            patch.ProxySwitch = o.Value<string>("proxy_switch") switch
            {
                "new" => SystemConfig.ProxySwitchAction.New,
                "add" => SystemConfig.ProxySwitchAction.Add,
                _ => SystemConfig.ProxySwitchAction.Off,
            };

        if (o.ContainsKey("name_format"))
            patch.NameFormat = o.Value<string>("name_format");

        return patch;
    }
}