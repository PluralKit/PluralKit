namespace Myriad.Types
{
    public record InteractionResponse
    {
        public enum ResponseType
        {
            Pong = 1,
            Acknowledge = 2,
            ChannelMessage = 3,
            ChannelMessageWithSource = 4,
            AckWithSource = 5
        }

        public ResponseType Type { get; init; }
        public InteractionApplicationCommandCallbackData? Data { get; init; }
    }
}