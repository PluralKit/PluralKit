using Myriad.Types;

namespace Myriad.Gateway
{
    public record GuildRoleCreateEvent(ulong GuildId, Role Role): IGatewayEvent;
}