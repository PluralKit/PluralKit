#nullable enable
using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core;

public class SystemPatch: PatchObject
{
    public Partial<string?> Name { get; set; }
    public Partial<string?> Hid { get; set; }
    public Partial<string?> Description { get; set; }
    public Partial<string?> Tag { get; set; }
    public Partial<string?> Pronouns { get; set; }
    public Partial<string?> AvatarUrl { get; set; }
    public Partial<string?> BannerImage { get; set; }
    public Partial<string?> Color { get; set; }
    public Partial<string?> Token { get; set; }
    public Partial<string?> WebhookUrl { get; set; }
    public Partial<string?> WebhookToken { get; set; }
    public Partial<PrivacyLevel> NamePrivacy { get; set; }
    public Partial<PrivacyLevel> AvatarPrivacy { get; set; }
    public Partial<PrivacyLevel> DescriptionPrivacy { get; set; }
    public Partial<PrivacyLevel> MemberListPrivacy { get; set; }
    public Partial<PrivacyLevel> GroupListPrivacy { get; set; }
    public Partial<PrivacyLevel> FrontPrivacy { get; set; }
    public Partial<PrivacyLevel> FrontHistoryPrivacy { get; set; }
    public Partial<PrivacyLevel> PronounPrivacy { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("name", Name)
        .With("hid", Hid)
        .With("description", Description)
        .With("tag", Tag)
        .With("pronouns", Pronouns)
        .With("avatar_url", AvatarUrl)
        .With("banner_image", BannerImage)
        .With("color", Color)
        .With("token", Token)
        .With("webhook_url", WebhookUrl)
        .With("webhook_token", WebhookToken)
        .With("name_privacy", NamePrivacy)
        .With("avatar_privacy", AvatarPrivacy)
        .With("description_privacy", DescriptionPrivacy)
        .With("member_list_privacy", MemberListPrivacy)
        .With("group_list_privacy", GroupListPrivacy)
        .With("front_privacy", FrontPrivacy)
        .With("front_history_privacy", FrontHistoryPrivacy)
        .With("pronoun_privacy", PronounPrivacy)
    );

    public new void AssertIsValid()
    {
        if (Name.Value != null)
            AssertValid(Name.Value, "name", Limits.MaxSystemNameLength);
        if (Description.Value != null)
            AssertValid(Description.Value, "description", Limits.MaxDescriptionLength);
        if (Tag.Value != null)
            AssertValid(Tag.Value, "tag", Limits.MaxSystemTagLength);
        if (Pronouns.Value != null)
            AssertValid(Pronouns.Value, "pronouns", Limits.MaxPronounsLength);
        if (AvatarUrl.Value != null)
            AssertValid(AvatarUrl.Value, "avatar_url", Limits.MaxUriLength,
                s => MiscUtils.TryMatchUri(s, out var avatarUri));
        if (BannerImage.Value != null)
            AssertValid(BannerImage.Value, "banner", Limits.MaxUriLength,
                s => MiscUtils.TryMatchUri(s, out var bannerUri));
        if (Color.Value != null)
            AssertValid(Color.Value, "color", "^[0-9a-fA-F]{6}$");
    }

#nullable disable

    public static SystemPatch FromJSON(JObject o, bool isImport = false)
    {
        var patch = new SystemPatch();
        if (o.ContainsKey("name")) patch.Name = o.Value<string>("name").NullIfEmpty();
        if (o.ContainsKey("description")) patch.Description = o.Value<string>("description").NullIfEmpty();
        if (o.ContainsKey("tag")) patch.Tag = o.Value<string>("tag").NullIfEmpty();
        if (o.ContainsKey("pronouns")) patch.Pronouns = o.Value<string>("pronouns").NullIfEmpty();
        if (o.ContainsKey("avatar_url")) patch.AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty();
        if (o.ContainsKey("banner")) patch.BannerImage = o.Value<string>("banner").NullIfEmpty();
        if (o.ContainsKey("color")) patch.Color = o.Value<string>("color").NullIfEmpty();

        if (isImport)
        {
            if (o.ContainsKey("description_privacy"))
                patch.DescriptionPrivacy = patch.ParsePrivacy(o, "description_privacy");
            if (o.ContainsKey("member_list_privacy"))
                patch.MemberListPrivacy = patch.ParsePrivacy(o, "member_list_privacy");
            if (o.ContainsKey("front_privacy")) patch.FrontPrivacy = patch.ParsePrivacy(o, "front_privacy");
            if (o.ContainsKey("front_history_privacy"))
                patch.FrontHistoryPrivacy = patch.ParsePrivacy(o, "front_history_privacy");
        }

        if (o.ContainsKey("privacy") && o["privacy"].Type != JTokenType.Null)
        {
            var privacy = o.Value<JObject>("privacy");

            if (privacy.ContainsKey("name_privacy"))
                patch.NamePrivacy = patch.ParsePrivacy(privacy, "name_privacy");

            if (privacy.ContainsKey("avatar_privacy"))
                patch.AvatarPrivacy = patch.ParsePrivacy(privacy, "avatar_privacy");

            if (privacy.ContainsKey("description_privacy"))
                patch.DescriptionPrivacy = patch.ParsePrivacy(privacy, "description_privacy");

            if (privacy.ContainsKey("pronoun_privacy"))
                patch.PronounPrivacy = patch.ParsePrivacy(privacy, "pronoun_privacy");

            if (privacy.ContainsKey("member_list_privacy"))
                patch.MemberListPrivacy = patch.ParsePrivacy(privacy, "member_list_privacy");

            if (privacy.ContainsKey("group_list_privacy"))
                patch.GroupListPrivacy = patch.ParsePrivacy(privacy, "group_list_privacy");

            if (privacy.ContainsKey("front_privacy"))
                patch.FrontPrivacy = patch.ParsePrivacy(privacy, "front_privacy");

            if (privacy.ContainsKey("front_history_privacy"))
                patch.FrontHistoryPrivacy = patch.ParsePrivacy(privacy, "front_history_privacy");
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
        if (Description.IsPresent)
            o.Add("description", Description.Value);
        if (Tag.IsPresent)
            o.Add("tag", Tag.Value);
        if (Pronouns.IsPresent)
            o.Add("pronouns", Pronouns.Value);
        if (AvatarUrl.IsPresent)
            o.Add("avatar_url", AvatarUrl.Value);
        if (BannerImage.IsPresent)
            o.Add("banner", BannerImage.Value);
        if (Color.IsPresent)
            o.Add("color", Color.Value);

        if (
            NamePrivacy.IsPresent
            || AvatarPrivacy.IsPresent
            || DescriptionPrivacy.IsPresent
            || PronounPrivacy.IsPresent
            || MemberListPrivacy.IsPresent
            || GroupListPrivacy.IsPresent
            || FrontPrivacy.IsPresent
            || FrontHistoryPrivacy.IsPresent
        )
        {
            var p = new JObject();

            if (NamePrivacy.IsPresent)
                p.Add("name_privacy", NamePrivacy.Value.ToJsonString());

            if (AvatarPrivacy.IsPresent)
                p.Add("avatar_privacy", AvatarPrivacy.Value.ToJsonString());

            if (DescriptionPrivacy.IsPresent)
                p.Add("description_privacy", DescriptionPrivacy.Value.ToJsonString());

            if (PronounPrivacy.IsPresent)
                p.Add("pronoun_privacy", PronounPrivacy.Value.ToJsonString());

            if (MemberListPrivacy.IsPresent)
                p.Add("member_list_privacy", MemberListPrivacy.Value.ToJsonString());

            if (GroupListPrivacy.IsPresent)
                p.Add("group_list_privacy", GroupListPrivacy.Value.ToJsonString());

            if (FrontPrivacy.IsPresent)
                p.Add("front_privacy", FrontPrivacy.Value.ToJsonString());

            if (FrontHistoryPrivacy.IsPresent)
                p.Add("front_history_privacy", FrontHistoryPrivacy.Value.ToJsonString());

            o.Add("privacy", p);
        }

        return o;
    }
}