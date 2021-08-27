using System.Text.Json.Serialization;

using Myriad.Serialization;

namespace Myriad.Rest.Types
{
    public record AllowedMentions
    {
        [JsonConverter(typeof(JsonSnakeCaseStringEnumConverter))]
        public enum ParseType
        {
            Roles,
            Users,
            Everyone
        }

        public ParseType[]? Parse { get; set; }
        public ulong[]? Users { get; set; }
        public ulong[]? Roles { get; set; }
        public bool RepliedUser { get; set; }
    }
}