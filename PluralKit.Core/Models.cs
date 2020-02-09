using System;
using System.Collections.Generic;
using System.Linq;

using Dapper.Contrib.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NodaTime;
using NodaTime.Text;

using PluralKit.Core;

namespace PluralKit
{
    public class PKParseError: Exception
    {
        public PKParseError(string message): base(message) { }
    }

    public enum PrivacyLevel
    {
        Public = 1,
        Private = 2
    }

    public static class PrivacyExt
    {
        public static bool CanAccess(this PrivacyLevel level, LookupContext ctx) =>
            level == PrivacyLevel.Public || ctx == LookupContext.ByOwner;
    }

    public enum LookupContext
    {
        ByOwner,
        ByNonOwner,
        API
    }
    
    public struct ProxyTag
    {
        public ProxyTag(string prefix, string suffix)
        {
            // Normalize empty strings to null for DB
            Prefix = prefix?.Length == 0 ? null : prefix;
            Suffix = suffix?.Length == 0 ? null : suffix;
        }

        [JsonProperty("prefix")] public string Prefix { get; set; }
        [JsonProperty("suffix")] public string Suffix { get; set; }

        [JsonIgnore] public string ProxyString => $"{Prefix ?? ""}text{Suffix ?? ""}";

        public bool IsEmpty => Prefix == null && Suffix == null;

        public bool Equals(ProxyTag other) => Prefix == other.Prefix && Suffix == other.Suffix;

        public override bool Equals(object obj) => obj is ProxyTag other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Prefix != null ? Prefix.GetHashCode() : 0) * 397) ^
                       (Suffix != null ? Suffix.GetHashCode() : 0);
            }
        }
    }

    public class PKSystem
    {
        // Additions here should be mirrored in SystemStore::Save
        [Key] [JsonIgnore] public int Id { get; set; }
        [JsonProperty("id")] public string Hid { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("tag")] public string Tag { get; set; }
        [JsonProperty("avatar_url")] public string AvatarUrl { get; set; }
        [JsonIgnore] public string Token { get; set; }
        [JsonProperty("created")] public Instant Created { get; set; }
        [JsonProperty("tz")] public string UiTz { get; set; }
        public PrivacyLevel DescriptionPrivacy { get; set; }
        public PrivacyLevel MemberListPrivacy { get; set; }
        public PrivacyLevel FrontPrivacy { get; set; }
        public PrivacyLevel FrontHistoryPrivacy { get; set; }
        
        [JsonIgnore] public DateTimeZone Zone => DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz);

        public JObject ToJson(LookupContext ctx)
        {
            var o = new JObject();
            o.Add("id", Hid);
            o.Add("name", Name);
            o.Add("description", DescriptionPrivacy.CanAccess(ctx) ? Description : null);
            o.Add("tag", Tag);
            o.Add("avatar_url", AvatarUrl);
            o.Add("created", Formats.TimestampExportFormat.Format(Created));
            o.Add("tz", UiTz);
            return o;
        }

        public void Apply(JObject o)
        {
            if (o.ContainsKey("name")) Name = o.Value<string>("name").NullIfEmpty().BoundsCheck(Limits.MaxSystemNameLength, "System name");
            if (o.ContainsKey("description")) Description = o.Value<string>("description").NullIfEmpty().BoundsCheck(Limits.MaxDescriptionLength, "System description");
            if (o.ContainsKey("tag")) Tag = o.Value<string>("tag").NullIfEmpty().BoundsCheck(Limits.MaxSystemTagLength, "System tag");
            if (o.ContainsKey("avatar_url")) AvatarUrl = o.Value<string>("avatar_url").NullIfEmpty();
            if (o.ContainsKey("tz")) UiTz = o.Value<string>("tz") ?? "UTC";
        }
    }

    public class PKMember
    {
        // Additions here should be mirrored in MemberStore::Save
        [JsonIgnore] public int Id { get; set; }
        [JsonProperty("id")] public string Hid { get; set; }
        [JsonIgnore] public int System { get; set; }
        [JsonProperty("color")] public string Color { get; set; }
        [JsonProperty("avatar_url")] public string AvatarUrl { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("display_name")] public string DisplayName { get; set; }
        [JsonProperty("birthday")] public LocalDate? Birthday { get; set; }
        [JsonProperty("pronouns")] public string Pronouns { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("proxy_tags")] public ICollection<ProxyTag> ProxyTags { get; set; }
        [JsonProperty("keep_proxy")] public bool KeepProxy { get; set; }
        [JsonProperty("created")] public Instant Created { get; set; }

        public PrivacyLevel MemberPrivacy { get; set; }

        /// Returns a formatted string representing the member's birthday, taking into account that a year of "0001" or "0004" is hidden
        /// Before Feb 10 2020, the sentinel year was 0001, now it is 0004.
        [JsonIgnore] public string BirthdayString
        {
            get
            {
                if (Birthday == null) return null;

                var format = LocalDatePattern.CreateWithInvariantCulture("MMM dd, yyyy");
                if (Birthday?.Year == 1 || Birthday?.Year == 4) format = LocalDatePattern.CreateWithInvariantCulture("MMM dd");
                return format.Format(Birthday.Value);
            }
        }

        [JsonIgnore] public bool HasProxyTags => ProxyTags.Count > 0;
        public string ProxyName(string systemTag, string guildDisplayName)
        {
            if (systemTag == null) return guildDisplayName ?? DisplayName ?? Name;
            return $"{guildDisplayName ?? DisplayName ?? Name} {systemTag}";
        }

        public JObject ToJson(LookupContext ctx)
        {
            var o = new JObject();
            o.Add("id", Hid);
            o.Add("name", Name);
            o.Add("color", MemberPrivacy.CanAccess(ctx) ? Color : null);
            o.Add("display_name", DisplayName);
            o.Add("birthday", MemberPrivacy.CanAccess(ctx) && Birthday.HasValue ? Formats.DateExportFormat.Format(Birthday.Value) : null);
            o.Add("pronouns", MemberPrivacy.CanAccess(ctx) ? Pronouns : null);
            o.Add("avatar_url", AvatarUrl);
            o.Add("description", MemberPrivacy.CanAccess(ctx) ? Description : null);
            
            var tagArray = new JArray();
            foreach (var tag in ProxyTags) 
                tagArray.Add(new JObject {{"prefix", tag.Prefix}, {"suffix", tag.Suffix}});
            o.Add("proxy_tags", tagArray);

            o.Add("keep_proxy", KeepProxy);
            o.Add("created", Formats.TimestampExportFormat.Format(Created));

            if (ProxyTags.Count > 0)
            {
                // Legacy compatibility only, TODO: remove at some point
                o.Add("prefix", ProxyTags?.FirstOrDefault().Prefix);
                o.Add("suffix", ProxyTags?.FirstOrDefault().Suffix);
            }

            return o;
        }

        public void Apply(JObject o)
        {
            if (o.ContainsKey("name") && o["name"].Type == JTokenType.Null) 
                throw new PKParseError("Member name can not be set to null.");
            
            if (o.ContainsKey("name")) Name = o.Value<string>("name").BoundsCheck(Limits.MaxMemberNameLength, "Member name");
            if (o.ContainsKey("color")) Color = o.Value<string>("color").NullIfEmpty();
            if (o.ContainsKey("display_name")) DisplayName = o.Value<string>("display_name").NullIfEmpty().BoundsCheck(Limits.MaxMemberNameLength, "Member display name");
            if (o.ContainsKey("birthday"))
            {
                var str = o.Value<string>("birthday").NullIfEmpty();
                var res = Formats.DateExportFormat.Parse(str);
                if (res.Success) Birthday = res.Value;
                else if (str == null) Birthday = null;
                else throw new PKParseError("Could not parse member birthday.");
            }

            if (o.ContainsKey("pronouns")) Pronouns = o.Value<string>("pronouns").NullIfEmpty().BoundsCheck(Limits.MaxPronounsLength, "Member pronouns");
            if (o.ContainsKey("description")) Description = o.Value<string>("description").NullIfEmpty().BoundsCheck(Limits.MaxDescriptionLength, "Member descriptoin");
            if (o.ContainsKey("keep_proxy")) KeepProxy = o.Value<bool>("keep_proxy");

            if (o.ContainsKey("prefix") || o.ContainsKey("suffix") && !o.ContainsKey("proxy_tags"))
                ProxyTags = new[] {new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix"))};
            else if (o.ContainsKey("proxy_tags"))
            {
                ProxyTags = o.Value<JArray>("proxy_tags")
                    .OfType<JObject>().Select(o => new ProxyTag(o.Value<string>("prefix"), o.Value<string>("suffix")))
                    .ToList();
            }
        }
    }

    public class PKSwitch
    {
        public int Id { get; set; }
        public int System { get; set; }
        public Instant Timestamp { get; set; }
    }

    public class PKSwitchMember
    {
        public int Id { get; set; }
        public int Switch { get; set; }
        public int Member { get; set; }
    }
}