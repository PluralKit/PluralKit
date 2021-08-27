using Myriad.Types;

namespace Myriad.Rest.Types.Requests
{
    public record ExecuteWebhookRequest
    {
        public string? Content { get; init; }
        public string? Username { get; init; }
        public string? AvatarUrl { get; init; }
        public Embed[] Embeds { get; init; }
        public AllowedMentions? AllowedMentions { get; init; }
    }
}