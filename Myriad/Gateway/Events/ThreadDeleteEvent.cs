using Myriad.Types;

namespace Myriad.Gateway
{
    public record ThreadDeleteEvent: IGatewayEvent
    {
        public ulong Id { get; init; }
        public ulong? GuildId { get; init; }
        public ulong? ParentId { get; init; }
        public Channel.ChannelType Type { get; init; }
    }
}