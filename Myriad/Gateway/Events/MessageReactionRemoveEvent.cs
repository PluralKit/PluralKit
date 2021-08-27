using Myriad.Types;

namespace Myriad.Gateway
{
    public record MessageReactionRemoveEvent
        (ulong UserId, ulong ChannelId, ulong MessageId, ulong? GuildId, Emoji Emoji): IGatewayEvent;
}