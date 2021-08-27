using Myriad.Types;
using Myriad.Utils;

namespace Myriad.Gateway
{
    public record MessageUpdateEvent(ulong Id, ulong ChannelId): IGatewayEvent
    {
        public Optional<string?> Content { get; init; }
        public Optional<User> Author { get; init; }
        public Optional<GuildMemberPartial> Member { get; init; }
        public Optional<Message.Attachment[]> Attachments { get; init; }
        public Optional<ulong?> GuildId { get; init; }
        // TODO: lots of partials
    }
}