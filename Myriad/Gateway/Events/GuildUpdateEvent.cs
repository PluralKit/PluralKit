using Myriad.Types;

namespace Myriad.Gateway
{
    public record GuildUpdateEvent: Guild, IGatewayEvent;
}