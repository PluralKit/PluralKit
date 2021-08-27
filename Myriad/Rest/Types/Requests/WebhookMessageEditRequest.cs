using System.Text.Json.Serialization;

using Myriad.Utils;

namespace Myriad.Rest.Types.Requests
{
    public record WebhookMessageEditRequest
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<string?> Content { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<AllowedMentions> AllowedMentions { get; init; }
    }
}