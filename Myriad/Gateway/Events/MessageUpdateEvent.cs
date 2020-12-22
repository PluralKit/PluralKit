namespace Myriad.Gateway
{
    public record MessageUpdateEvent(ulong Id, ulong ChannelId): IGatewayEvent
    {
        // TODO: lots of partials
    }
}