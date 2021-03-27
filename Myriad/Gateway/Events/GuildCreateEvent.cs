using System.Collections.Generic;

using Myriad.Types;

namespace Myriad.Gateway
{
    public record GuildCreateEvent: Guild, IGatewayEvent
    {
        public Channel[] Channels { get; init; }
        public GuildMember[] Members { get; init; }
    }
}