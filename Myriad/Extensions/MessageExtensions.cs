using Myriad.Gateway;
using Myriad.Types;

namespace Myriad.Extensions
{
    public static class MessageExtensions
    {
        public static string JumpLink(this Message msg) =>
            $"https://discord.com/channels/{msg.GuildId}/{msg.ChannelId}/{msg.Id}";

        public static string JumpLink(this MessageReactionAddEvent msg) =>
            $"https://discord.com/channels/{msg.GuildId}/{msg.ChannelId}/{msg.MessageId}";
    }
}