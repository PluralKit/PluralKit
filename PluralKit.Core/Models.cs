using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using Dapper.Contrib.Extensions;
using Newtonsoft.Json;
using NodaTime;
using NodaTime.Text;

namespace PluralKit
{
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
        [JsonIgnore] public DateTimeZone Zone => DateTimeZoneProviders.Tzdb.GetZoneOrNull(UiTz);

        public void ToJson(System.Text.Json.Utf8JsonWriter w)
        {
            w.WriteStartObject();
            w.WriteString("id", Hid);
            w.WriteString("description", Description);
            w.WriteString("tag", Tag);
            w.WriteString("avatar_url", AvatarUrl);
            w.WriteString("created", Formats.TimestampExportFormat.Format(Created));
            w.WriteString("tz", UiTz);
            w.WriteEndObject();
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
        
        // These are deprecated as fuck, and are kinda hacky
        // Don't use, unless you're the API's serialization library
        [JsonProperty("prefix")] [Obsolete("Use PKMember.ProxyTags")] public string Prefix
        {
            get => ProxyTags?.FirstOrDefault().Prefix;
            set => ProxyTags = new[] {new ProxyTag(Prefix, value)};
        }

        [JsonProperty("suffix")] [Obsolete("Use PKMember.ProxyTags")] public string Suffix
        {
            get => ProxyTags?.FirstOrDefault().Suffix;
            set => ProxyTags = new[] {new ProxyTag(Suffix, value)};
        }

        /// Returns a formatted string representing the member's birthday, taking into account that a year of "0001" is hidden
        [JsonIgnore] public string BirthdayString
        {
            get
            {
                if (Birthday == null) return null;

                var format = LocalDatePattern.CreateWithInvariantCulture("MMM dd, yyyy");
                if (Birthday?.Year == 1) format = LocalDatePattern.CreateWithInvariantCulture("MMM dd");
                return format.Format(Birthday.Value);
            }
        }

        [JsonIgnore] public bool HasProxyTags => ProxyTags.Count > 0;
        public string ProxyName(string systemTag)
        {
            if (systemTag == null) return DisplayName ?? Name;
            return $"{DisplayName ?? Name} {systemTag}";
        }

        public void ToJson(Utf8JsonWriter w)
        {
            w.WriteStartObject();
            w.WriteString("id", Hid);
            w.WriteString("name", Name);
            w.WriteString("color", Color);
            w.WriteString("display_name", DisplayName);
            w.WriteString("birthday", Birthday.HasValue ? Formats.DateExportFormat.Format(Birthday.Value) : null);
            w.WriteString("pronouns", Pronouns);
            w.WriteString("description", Description);
            w.WriteStartArray("proxy_tags");
            foreach (var tag in ProxyTags)
            {
                w.WriteStartObject();
                w.WriteString("prefix", tag.Prefix);
                w.WriteString("suffix", tag.Suffix);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteBoolean("keep_proxy", KeepProxy);
            w.WriteString("created", Formats.TimestampExportFormat.Format(Created));

            if (ProxyTags.Count > 0)
            {
                // Legacy compatibility only, TODO: remove at some point
                w.WriteString("prefix", ProxyTags?.FirstOrDefault().Prefix);
                w.WriteString("suffix", ProxyTags?.FirstOrDefault().Suffix);
            }

            w.WriteEndObject();
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