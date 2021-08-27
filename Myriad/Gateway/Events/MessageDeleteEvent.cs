namespace Myriad.Gateway
{
    public record MessageDeleteEvent(ulong Id, ulong ChannelId, ulong? GuildId): IGatewayEvent;
}