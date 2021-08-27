using Myriad.Types;

namespace Myriad.Gateway
{
    public record MessageReactionAddEvent(ulong UserId, ulong ChannelId, ulong MessageId, ulong? GuildId,
                                          GuildMember? Member,
                                          Emoji Emoji): IGatewayEvent;
}