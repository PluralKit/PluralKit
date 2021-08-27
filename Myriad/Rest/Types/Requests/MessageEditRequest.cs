using System.Text.Json.Serialization;

using Myriad.Types;
using Myriad.Utils;

namespace Myriad.Rest.Types.Requests
{
    public record MessageEditRequest
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<string?> Content { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<Embed?> Embed { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<Message.MessageFlags> Flags { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<AllowedMentions> AllowedMentions { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<MessageComponent[]?> Components { get; init; }
    }
}