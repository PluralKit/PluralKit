using System.Collections.Generic;

using Myriad.Rest.Types;

namespace Myriad.Types
{
    public record InteractionApplicationCommandCallbackData
    {
        public bool? Tts { get; init; }
        public string Content { get; init; }
        public Embed[]? Embeds { get; init; }
        public AllowedMentions? AllowedMentions { get; init; }
        public Message.MessageFlags Flags { get; init; }
    }
}