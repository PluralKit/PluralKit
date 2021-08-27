using Myriad.Types;

namespace Myriad.Gateway
{
    public record GuildMemberUpdateEvent: GuildMember, IGatewayEvent
    {
        public ulong GuildId { get; init; }
    }
}