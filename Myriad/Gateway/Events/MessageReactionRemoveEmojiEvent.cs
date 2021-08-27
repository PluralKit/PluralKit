using Myriad.Types;

namespace Myriad.Gateway
{
    public record MessageReactionRemoveEmojiEvent
        (ulong ChannelId, ulong MessageId, ulong? GuildId, Emoji Emoji): IGatewayEvent;
}