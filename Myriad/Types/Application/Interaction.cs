namespace Myriad.Types
{
    public record Interaction
    {
        public enum InteractionType
        {
            Ping = 1,
            ApplicationCommand = 2
        }

        public ulong Id { get; init; }
        public InteractionType Type { get; init; }
        public ApplicationCommandInteractionData? Data { get; init; }
        public ulong GuildId { get; init; }
        public ulong ChannelId { get; init; }
        public GuildMember Member { get; init; }
        public string Token { get; init; }
    }
}