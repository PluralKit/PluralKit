using Myriad.Types;

namespace Myriad.Gateway
{
    public record ChannelUpdateEvent: Channel, IGatewayEvent;
}