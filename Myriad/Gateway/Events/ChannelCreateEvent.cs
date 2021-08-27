using Myriad.Types;

namespace Myriad.Gateway
{
    public record ChannelCreateEvent: Channel, IGatewayEvent;
}