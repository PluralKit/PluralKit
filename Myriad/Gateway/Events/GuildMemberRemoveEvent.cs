using Myriad.Types;

namespace Myriad.Gateway
{
    public class GuildMemberRemoveEvent: IGatewayEvent
    {
        public ulong GuildId { get; init; }
        public User User { get; init; }
    }
}