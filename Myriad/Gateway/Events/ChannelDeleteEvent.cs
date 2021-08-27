using Myriad.Types;

namespace Myriad.Gateway
{
    public record ChannelDeleteEvent: Channel, IGatewayEvent;
}