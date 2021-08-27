using Myriad.Types;

namespace Myriad.Gateway
{
    public record MessageCreateEvent: Message, IGatewayEvent
    {
        public GuildMemberPartial? Member { get; init; }
    }
}