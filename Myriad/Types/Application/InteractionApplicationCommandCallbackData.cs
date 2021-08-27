using System.Text.Json.Serialization;

using Myriad.Rest.Types;
using Myriad.Utils;

namespace Myriad.Types
{
    public record InteractionApplicationCommandCallbackData
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<bool?> Tts { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<string?> Content { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<Embed[]?> Embeds { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<AllowedMentions?> AllowedMentions { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<Message.MessageFlags> Flags { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Optional<MessageComponent[]?> Components { get; init; }
    }
}