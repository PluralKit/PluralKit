using Myriad.Utils;

namespace Myriad.Gateway
{
    public record MessageUpdateEvent(ulong Id, ulong ChannelId): IGatewayEvent
    {
        public Optional<string?> Content { get; init; }
        // TODO: lots of partials
    }
}