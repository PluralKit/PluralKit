namespace Myriad.Gateway
{
    public record MessageDeleteBulkEvent(ulong[] Ids, ulong ChannelId, ulong? GuildId): IGatewayEvent;
}