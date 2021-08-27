namespace Myriad.Types
{
    public record Webhook
    {
        public ulong Id { get; init; }
        public WebhookType Type { get; init; }
        public ulong? GuildId { get; init; }
        public ulong ChannelId { get; init; }
        public User? User { get; init; }
        public string? Name { get; init; }
        public string? Avatar { get; init; }
        public string? Token { get; init; }
        public ulong? ApplicationId { get; init; }
    }

    public enum WebhookType
    {
        Incoming = 1,
        ChannelFollower = 2
    }
}