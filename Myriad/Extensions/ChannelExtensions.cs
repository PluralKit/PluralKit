using Myriad.Types;

namespace Myriad.Extensions
{
    public static class ChannelExtensions
    {
        public static string Mention(this Channel channel) => $"<#{channel.Id}>";

        public static bool IsThread(this Channel channel) => channel.Type.IsThread();

        public static bool IsThread(this Channel.ChannelType type) =>
            type is Channel.ChannelType.GuildPublicThread
                or Channel.ChannelType.GuildPrivateThread
                or Channel.ChannelType.GuildNewsThread;
    }
}