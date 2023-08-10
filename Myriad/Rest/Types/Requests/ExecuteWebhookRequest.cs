using Myriad.Types;

namespace Myriad.Rest.Types.Requests;

public record ExecuteWebhookRequest
{
    public string? Content { get; init; }
    public string? Username { get; init; }
    public string? AvatarUrl { get; init; }
    public Embed[] Embeds { get; init; }
    public Sticker[] Stickers { get; init; }
    public Message.Attachment[] Attachments { get; set; }
    public AllowedMentions? AllowedMentions { get; init; }
    public bool? Tts { get; init; }
    public Message.MessageFlags? Flags { get; set; }
}