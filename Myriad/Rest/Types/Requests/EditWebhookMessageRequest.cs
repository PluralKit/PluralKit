using Myriad.Types;

namespace Myriad.Rest.Types.Requests
{
    public record EditWebhookMessageRequest
    {
        public string Content { get; init; }
        public Embed[] Embeds { get; init; }
        public AllowedMentions? AllowedMentions { get; init; }
    }
}
