using Myriad.Types;

namespace Myriad.Gateway
{
    public record ThreadListSyncEvent: IGatewayEvent
    {
        public ulong GuildId { get; init; }
        public ulong[]? ChannelIds { get; init; }
        public Channel[] Threads { get; init; }
    }
}