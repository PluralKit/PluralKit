using Newtonsoft.Json.Linq;

using NodaTime;

using SqlKata;

namespace PluralKit.Core;

public class SystemConfigPatch: PatchObject
{
    public Partial<string> UiTz { get; set; }
    public Partial<bool> PingsEnabled { get; set; }
    public Partial<int?> LatchTimeout { get; set; }
    public Partial<PrivacyLevel> MemberDefaultPrivacy { get; set; }
    public Partial<PrivacyLevel> GroupDefaultPrivacy { get; set; }
    public Partial<PrivacyLevel> DefaultPrivacyShown { get; set; }
    public Partial<int?> MemberLimitOverride { get; set; }
    public Partial<int?> GroupLimitOverride { get; set; }
    public Partial<string[]> DescriptionTemplates { get; set; }
    public Partial<bool> CaseSensitiveProxyTags { get; set; }
    public Partial<bool> ProxyErrorMessageEnabled { get; set; }


    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("ui_tz", UiTz)
        .With("pings_enabled", PingsEnabled)
        .With("latch_timeout", LatchTimeout)
        .With("member_default_privacy", MemberDefaultPrivacy)
        .With("group_default_privacy", GroupDefaultPrivacy)
        .With("default_privacy_shown", DefaultPrivacyShown)
        .With("member_limit_override", MemberLimitOverride)
        .With("group_limit_override", GroupLimitOverride)
        .With("description_templates", DescriptionTemplates)
        .With("case_sensitive_proxy_tags", CaseSensitiveProxyTags)
        .With("proxy_error_message_enabled", ProxyErrorMessageEnabled)
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

        if (MemberDefaultPrivacy.IsPresent)
            o.Add("member_default_privacy", MemberDefaultPrivacy.Value.ToJsonString());

        if (GroupDefaultPrivacy.IsPresent)
            o.Add("group_default_private", GroupDefaultPrivacy.Value.ToJsonString());

        if (DefaultPrivacyShown.IsPresent)
            o.Add("default_privacy_shown", DefaultPrivacyShown.Value.ToJsonString());

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

        return o;
    }

    public static SystemConfigPatch FromJson(JObject o, bool isImport = false)
    {
        var patch = new SystemConfigPatch();

        if (o.ContainsKey("timezone"))
            patch.UiTz = o.Value<string>("timezone");

        if (o.ContainsKey("pings_enabled"))
            patch.PingsEnabled = o.Value<bool>("pings_enabled");

        if (o.ContainsKey("latch_timeout"))
            patch.LatchTimeout = o.Value<int?>("latch_timeout");

        if (isImport)
        {
            // legacy: used in old export files
            if (o.ContainsKey("member_default_private"))
                patch.MemberDefaultPrivacy = o.Value<bool>("member_default_private") ? PrivacyLevel.Private : PrivacyLevel.Public;

            if (o.ContainsKey("group_default_private"))
                patch.GroupDefaultPrivacy = o.Value<bool>("group_default_private") ? PrivacyLevel.Private : PrivacyLevel.Public;
        }

        if (o.ContainsKey("member_default_privacy"))
            patch.MemberDefaultPrivacy = patch.ParsePrivacy(o, "member_default_privacy");

        if (o.ContainsKey("group_default_privacy"))
            patch.GroupDefaultPrivacy = patch.ParsePrivacy(o, "group_default_privacy");

        if (o.ContainsKey("description_templates"))
            patch.DescriptionTemplates = o.Value<JArray>("description_templates").Select(x => x.Value<string>()).ToArray();

        if (o.ContainsKey("case_sensitive_proxy_tags"))
            patch.CaseSensitiveProxyTags = o.Value<bool>("case_sensitive_proxy_tags");

        if (o.ContainsKey("proxy_error_message_enabled"))
            patch.ProxyErrorMessageEnabled = o.Value<bool>("proxy_error_message_enabled");

        return patch;
    }
}