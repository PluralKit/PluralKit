using Myriad.Types;

namespace Myriad.Rest.Types.Requests
{
    public record MessageRequest
    {
        public string? Content { get; set; }
        public object? Nonce { get; set; }
        public bool Tts { get; set; }
        public AllowedMentions? AllowedMentions { get; set; }
        public Embed? Embed { get; set; }
        public MessageComponent[]? Components { get; set; }
    }
}