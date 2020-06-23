using Newtonsoft.Json;

namespace PluralKit.Core
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
}