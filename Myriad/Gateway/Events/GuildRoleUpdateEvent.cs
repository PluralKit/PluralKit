using Myriad.Types;

namespace Myriad.Gateway
{
    public record GuildRoleUpdateEvent(ulong GuildId, Role Role): IGatewayEvent;
}