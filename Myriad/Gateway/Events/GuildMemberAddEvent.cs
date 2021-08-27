using Myriad.Types;

namespace Myriad.Gateway
{
    public record GuildMemberAddEvent: GuildMember, IGatewayEvent
    {
        public ulong GuildId { get; init; }
    }
}