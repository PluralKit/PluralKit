namespace Myriad.Gateway
{
    public record GuildDeleteEvent(ulong Id, bool Unavailable): IGatewayEvent;
}