using Myriad.Types;

namespace Myriad.Gateway
{
    public record InteractionCreateEvent: Interaction, IGatewayEvent;
}