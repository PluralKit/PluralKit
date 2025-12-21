using Newtonsoft.Json.Linq;

using NodaTime;

namespace PluralKit.Core;

public class SystemConfig
{
    public SystemId Id { get; }
    public string UiTz { get; set; }
    public bool PingsEnabled { get; }
    public int? LatchTimeout { get; }
    public bool MemberDefaultPrivate { get; }
    public bool GroupDefaultPrivate { get; }
    public bool ShowPrivateInfo { get; }
    public int? MemberLimitOverride { get; }
    public int? GroupLimitOverride { get; }
    public ICollection<string> DescriptionTemplates { get; }

    public DateTimeZone Zone => DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz);

    public bool CaseSensitiveProxyTags { get; }
    public bool ProxyErrorMessageEnabled { get; }
    public bool HidDisplaySplit { get; }
    public bool HidDisplayCaps { get; }
    public bool CardShowColorHex { get; }
    public HidPadFormat HidListPadding { get; }
    public ProxySwitchAction ProxySwitch { get; }
    public string NameFormat { get; }

    public bool PremiumLifetime { get; }
    public Instant? PremiumUntil { get; }

    public enum HidPadFormat
    {
        None = 0,
        Left = 1,
        Right = 2,
    }
    public enum ProxySwitchAction
    {
        Off = 0,
        New = 1,
        Add = 2,
    }
}

public static class SystemConfigExt
{
    public static JObject ToJson(this SystemConfig cfg)
    {
        var o = new JObject();

        o.Add("timezone", cfg.UiTz);
        o.Add("pings_enabled", cfg.PingsEnabled);
        o.Add("latch_timeout", cfg.LatchTimeout);
        o.Add("member_default_private", cfg.MemberDefaultPrivate);
        o.Add("group_default_private", cfg.GroupDefaultPrivate);
        o.Add("show_private_info", cfg.ShowPrivateInfo);
        o.Add("member_limit", cfg.MemberLimitOverride ?? Limits.MaxMemberCount);
        o.Add("group_limit", cfg.GroupLimitOverride ?? Limits.MaxGroupCount);
        o.Add("case_sensitive_proxy_tags", cfg.CaseSensitiveProxyTags);
        o.Add("proxy_error_message_enabled", cfg.ProxyErrorMessageEnabled);
        o.Add("hid_display_split", cfg.HidDisplaySplit);
        o.Add("hid_display_caps", cfg.HidDisplayCaps);
        o.Add("hid_list_padding", cfg.HidListPadding.ToUserString());
        o.Add("card_show_color_hex", cfg.CardShowColorHex);
        o.Add("proxy_switch", cfg.ProxySwitch.ToUserString());
        o.Add("name_format", cfg.NameFormat);

        o.Add("description_templates", JArray.FromObject(cfg.DescriptionTemplates));

        return o;
    }

    public static string ToUserString(this SystemConfig.HidPadFormat val)
    {
        if (val == SystemConfig.HidPadFormat.None) return "off";
        return val.ToString().ToLower();
    }

    public static string ToUserString(this SystemConfig.ProxySwitchAction val) => val.ToString().ToLower();

}