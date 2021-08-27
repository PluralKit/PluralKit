namespace Myriad.Gateway
{
    public record MessageReactionRemoveAllEvent(ulong ChannelId, ulong MessageId, ulong? GuildId): IGatewayEvent;
}