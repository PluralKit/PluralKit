#nullable enable
using Newtonsoft.Json.Linq;

using NodaTime;

using SqlKata;

namespace PluralKit.Core;

public class MemberPatch: PatchObject
{
    public Partial<string> Name { get; set; }
    public Partial<string> Hid { get; set; }
    public Partial<string?> DisplayName { get; set; }
    public Partial<string?> WebhookAvatarUrl { get; set; }
    public Partial<string?> AvatarUrl { get; set; }
    public Partial<string?> BannerImage { get; set; }
    public Partial<string?> Color { get; set; }
    public Partial<LocalDate?> Birthday { get; set; }
    public Partial<string?> Pronouns { get; set; }
    public Partial<string?> Description { get; set; }
    public Partial<ProxyTag[]> ProxyTags { get; set; }
    public Partial<bool> KeepProxy { get; set; }
    public Partial<int> MessageCount { get; set; }
    public Partial<bool> AllowAutoproxy { get; set; }
    public Partial<PrivacyLevel> Visibility { get; set; }
    public Partial<PrivacyLevel> NamePrivacy { get; set; }
    public Partial<PrivacyLevel> DescriptionPrivacy { get; set; }
    public Partial<PrivacyLevel> PronounPrivacy { get; set; }
    public Partial<PrivacyLevel> BirthdayPrivacy { get; set; }
    public Partial<PrivacyLevel> AvatarPrivacy { get; set; }
    public Partial<PrivacyLevel> ProxyPrivacy { get; set; }
    public Partial<PrivacyLevel> MetadataPrivacy { get; set; }


    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("name", Name)
        .With("hid", Hid)
        .With("display_name", DisplayName)
        .With("webhook_avatar_url", WebhookAvatarUrl)
        .With("avatar_url", AvatarUrl)
        .With("banner_image", BannerImage)
        .With("color", Color)
        .With("birthday", Birthday)
        .With("pronouns", Pronouns)
        .With("description", Description)
        .With("proxy_tags", ProxyTags)
        .With("keep_proxy", KeepProxy)
        .With("message_count", MessageCount)
        .With("allow_autoproxy", AllowAutoproxy)
        .With("member_visibility", Visibility)
        .With("name_privacy", NamePrivacy)
        .With("description_privacy", DescriptionPrivacy)
        .With("pronoun_privacy", PronounPrivacy)
        .With("birthday_privacy", BirthdayPrivacy)
        .With("avatar_privacy", AvatarPrivacy)
        .With("proxy_privacy", ProxyPrivacy)
        .With("metadata_privacy", MetadataPrivacy)
    );

    public new void AssertIsValid()
    {
        if (Name.Value != null)
            AssertValid(Name.Value, "name", Limits.MaxMemberNameLength);
        if (DisplayName.Value != null)
            AssertValid(DisplayName.Value, "display_name", Limits.MaxMemberNameLength);
        if (AvatarUrl.Value != null)
            AssertValid(AvatarUrl.Value, "avatar_url", Limits.MaxUriLength,
                s => MiscUtils.TryMatchUri(s, out var avatarUri));
        if (WebhookAvatarUrl.Value != null)
            AssertValid(WebhookAvatarUrl.Value, "webhook_avatar_url", Limits.MaxUriLength,
                s => MiscUtils.TryMatchUri(s, out var webhookAvatarUri));
        if (BannerImage.Value != null)
            AssertValid(BannerImage.Value, "banner", Limits.MaxUriLength,
                s => MiscUtils.TryMatchUri(s, out var bannerUri));
        if (Color.Value != null)
            AssertValid(Color.Value, "color", "^[0-9a-fA-F]{6}$");
        if (Pronouns.Value != null)
            AssertValid(Pronouns.Value, "pronouns", Limits.MaxPronounsLength);
        if (Description.Value != null)
            AssertValid(Description.Value, "description", Limits.MaxDescriptionLength);
        if (ProxyTags.IsPresent && (ProxyTags.Value.Length > 100 ||
                                    ProxyTags.Value.Any(tag => tag.ProxyString.IsLongerThan(100))))
            // todo: have a better error for this
            Errors.Add(new ValidationError("proxy_tags"));
    }

#nullable disable

    public static MemberPatch FromJSON(JObject o, bool isImport = false)
    {
        var patch = new MemberPatch();

        if (o.ContainsKey("name"))
        {
            patch.Name = o.Value<string>("name").NullIfEmpty();
            if (patch.Name.Value == null)
                patch.Errors.Add(new ValidationError("name", "Member name can not be set to null."));
        }

        if (o.ContainsKey("name")) patch.Name = o.Value<string>("name");
        if (o.ContainsKey("color")) patch.Color = o.Value<string>("color").NullIfEmpty()?.ToLower();
        if (o.ContainsKey("display_name")) patch.DisplayName = o.Value<string>("display_name").NullIfEmpty();
        if (o.ContainsKey("avatar_url")) patch.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty();
        if (o.ContainsKey("webhook_avatar_url"))
        {
            var str = o.Value<string>("webhook_avatar_url").NullIfEmpty();
            // XXX: ignore webhook_avatar_url if it's exactly the same as avatar_url
            // to work around some export files containing the value of avatar_url in
            // both fields accidentally
            if (str != null && patch.AvatarUrl.Value != str) patch.WebhookAvatarUrl = str;
            else patch.WebhookAvatarUrl = null;
        }

        if (o.ContainsKey("banner")) patch.BannerImage = o.Value<string>("banner").NullIfEmpty();

        if (o.ContainsKey("birthday"))
        {
            var str = o.Value<string>("birthday").NullIfEmpty();
            var res = DateTimeFormats.DateExportFormat.Parse(str);
            if (res.Success) patch.Birthday = res.Value;
            else if (str == null) patch.Birthday = null;
            else patch.Errors.Add(new ValidationError("birthday"));
        }

        if (o.ContainsKey("pronouns")) patch.Pronouns = o.Value<string>("pronouns").NullIfEmpty();
        if (o.ContainsKey("description")) patch.Description = o.Value<string>("description").NullIfEmpty();
        if (o.ContainsKey("keep_proxy")) patch.KeepProxy = o.Value<bool>("keep_proxy");

        if (isImport)
        {
            // legacy: used in old export files
            if (o.ContainsKey("prefix") || o.ContainsKey("suffix") && !o.ContainsKey("proxy_tags"))
                patch.ProxyTags = new[] { new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix")) };

            if (o.ContainsKey("visibility")) patch.Visibility = patch.ParsePrivacy(o, "visibility");
            if (o.ContainsKey("name_privacy")) patch.NamePrivacy = patch.ParsePrivacy(o, "name_privacy");
            if (o.ContainsKey("description_privacy"))
                patch.DescriptionPrivacy = patch.ParsePrivacy(o, "description_privacy");
            if (o.ContainsKey("avatar_privacy"))
                patch.AvatarPrivacy = patch.ParsePrivacy(o, "avatar_privacy");
            if (o.ContainsKey("birthday_privacy"))
                patch.BirthdayPrivacy = patch.ParsePrivacy(o, "birthday_privacy");
            if (o.ContainsKey("pronoun_privacy"))
                patch.PronounPrivacy = patch.ParsePrivacy(o, "pronoun_privacy");
            if (o.ContainsKey("proxy_privacy"))
                patch.ProxyPrivacy = patch.ParsePrivacy(o, "proxy_privacy");
            if (o.ContainsKey("metadata_privacy"))
                patch.MetadataPrivacy = patch.ParsePrivacy(o, "metadata_privacy");
        }

        if (o.ContainsKey("proxy_tags"))
            patch.ProxyTags = o.Value<JArray>("proxy_tags")
                .OfType<JObject>().Select(o =>
                    new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix")))
                .Where(p => p.Valid)
                .ToArray();

        if (o.ContainsKey("privacy") && o["privacy"].Type == JTokenType.Object)
        {
            var privacy = o.Value<JObject>("privacy");

            if (privacy.ContainsKey("visibility"))
                patch.Visibility = patch.ParsePrivacy(privacy, "visibility");

            if (privacy.ContainsKey("name_privacy"))
                patch.NamePrivacy = patch.ParsePrivacy(privacy, "name_privacy");

            if (privacy.ContainsKey("description_privacy"))
                patch.DescriptionPrivacy = patch.ParsePrivacy(privacy, "description_privacy");

            if (privacy.ContainsKey("avatar_privacy"))
                patch.AvatarPrivacy = patch.ParsePrivacy(privacy, "avatar_privacy");

            if (privacy.ContainsKey("birthday_privacy"))
                patch.BirthdayPrivacy = patch.ParsePrivacy(privacy, "birthday_privacy");

            if (privacy.ContainsKey("pronoun_privacy"))
                patch.PronounPrivacy = patch.ParsePrivacy(privacy, "pronoun_privacy");

            if (privacy.ContainsKey("proxy_privacy"))
                patch.ProxyPrivacy = patch.ParsePrivacy(privacy, "proxy_privacy");

            if (privacy.ContainsKey("metadata_privacy"))
                patch.MetadataPrivacy = patch.ParsePrivacy(privacy, "metadata_privacy");
        }

        return patch;
    }

    public JObject ToJson()
    {
        var o = new JObject();

        if (Name.IsPresent)
            o.Add("name", Name.Value);
        if (Hid.IsPresent)
            o.Add("id", Hid.Value);
        if (DisplayName.IsPresent)
            o.Add("display_name", DisplayName.Value);
        if (AvatarUrl.IsPresent)
            o.Add("avatar_url", AvatarUrl.Value);
        if (WebhookAvatarUrl.IsPresent)
            o.Add("webhook_avatar_url", WebhookAvatarUrl.Value);
        if (BannerImage.IsPresent)
            o.Add("banner", BannerImage.Value);
        if (Color.IsPresent)
            o.Add("color", Color.Value);
        if (Birthday.IsPresent)
            o.Add("birthday", Birthday.Value?.FormatExport());
        if (Pronouns.IsPresent)
            o.Add("pronouns", Pronouns.Value);
        if (Description.IsPresent)
            o.Add("description", Description.Value);
        if (ProxyTags.IsPresent)
        {
            var tagArray = new JArray();
            foreach (var tag in ProxyTags.Value)
                tagArray.Add(new JObject { { "prefix", tag.Prefix }, { "suffix", tag.Suffix } });
            o.Add("proxy_tags", tagArray);
        }

        if (KeepProxy.IsPresent)
            o.Add("keep_proxy", KeepProxy.Value);

        if (
            Visibility.IsPresent
            || NamePrivacy.IsPresent
            || DescriptionPrivacy.IsPresent
            || PronounPrivacy.IsPresent
            || BirthdayPrivacy.IsPresent
            || AvatarPrivacy.IsPresent
            || ProxyPrivacy.IsPresent
            || MetadataPrivacy.IsPresent
        )
        {
            var p = new JObject();

            if (Visibility.IsPresent)
                p.Add("visibility", Visibility.Value.ToJsonString());

            if (NamePrivacy.IsPresent)
                p.Add("name_privacy", NamePrivacy.Value.ToJsonString());

            if (DescriptionPrivacy.IsPresent)
                p.Add("description_privacy", DescriptionPrivacy.Value.ToJsonString());

            if (PronounPrivacy.IsPresent)
                p.Add("pronoun_privacy", PronounPrivacy.Value.ToJsonString());

            if (BirthdayPrivacy.IsPresent)
                p.Add("birthday_privacy", BirthdayPrivacy.Value.ToJsonString());

            if (AvatarPrivacy.IsPresent)
                p.Add("avatar_privacy", AvatarPrivacy.Value.ToJsonString());

            if (ProxyPrivacy.IsPresent)
                p.Add("proxy_privacy", ProxyPrivacy.Value.ToJsonString());

            if (MetadataPrivacy.IsPresent)
                p.Add("metadata_privacy", MetadataPrivacy.Value.ToJsonString());

            o.Add("privacy", p);
        }

        return o;
    }
}